using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Durable PostgreSQL-backed broadcast worker. Recipient rows are claimed in small
/// batches before provider calls so HTTP requests never wait for a large mailing list.
/// </summary>
public sealed class EmailBroadcastDispatcher : BackgroundService
{
    private const int BatchSize = 50;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBroadcastDispatcher> _logger;

    public EmailBroadcastDispatcher(IServiceScopeFactory scopeFactory,
        ILogger<EmailBroadcastDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var worked = await DispatchOneBatchAsync(stoppingToken);
                await Task.Delay(worked ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(2),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Email broadcast dispatcher cycle failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    internal async Task<bool> DispatchOneBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<IIntegrationGateway>();
        var now = DateTime.UtcNow;

        var stuck = await context.EmailBroadcastRecipients.Where(x =>
                x.Status == EmailBroadcastRecipientStatus.Processing &&
                x.LastAttemptAt < now.AddMinutes(-15))
            .OrderBy(x => x.LastAttemptAt).ThenBy(x => x.Id)
            .Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var recipient in stuck)
        {
            recipient.Status = EmailBroadcastRecipientStatus.Pending;
            recipient.FailureReason = "Previous delivery attempt was interrupted and requeued.";
        }
        if (stuck.Count > 0) await context.SaveChangesAsync(cancellationToken);

        var campaignId = await context.EmailBroadcasts.AsNoTracking()
            .Where(x => (x.Status == EmailBroadcastStatus.Queued || x.Status == EmailBroadcastStatus.Sending ||
                         x.Status == EmailBroadcastStatus.Scheduled && x.ScheduledFor <= now) &&
                        (!x.ScheduledFor.HasValue || x.ScheduledFor <= now))
            .OrderBy(x => x.ScheduledFor ?? x.QueuedAt ?? x.CreatedAt)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (!campaignId.HasValue) return stuck.Count > 0;

        var strategy = context.Database.CreateExecutionStrategy();
        var claimedIds = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var lockKey = $"email-broadcast:{campaignId.Value:N}";
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
                var campaign = await context.EmailBroadcasts.FirstAsync(x => x.Id == campaignId.Value,
                    cancellationToken);
                if (campaign.Status is EmailBroadcastStatus.Cancelled or EmailBroadcastStatus.Sent)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new List<Guid>();
                }
                if (campaign.Status == EmailBroadcastStatus.Scheduled) campaign.Status = EmailBroadcastStatus.Queued;
                if (!campaign.StartedAt.HasValue) campaign.StartedAt = now;
                campaign.Status = EmailBroadcastStatus.Sending;
                var claimed = await context.EmailBroadcastRecipients.Where(x =>
                        x.EmailBroadcastId == campaign.Id && x.Status == EmailBroadcastRecipientStatus.Pending)
                    .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(BatchSize)
                    .ToListAsync(cancellationToken);
                foreach (var recipient in claimed)
                {
                    recipient.Status = EmailBroadcastRecipientStatus.Processing;
                    recipient.AttemptCount++;
                    recipient.LastAttemptAt = now;
                    recipient.FailureReason = null;
                }
                var ids = claimed.Select(x => x.Id).ToList();
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ids;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        if (claimedIds.Count == 0)
        {
            await RefreshCampaignAsync(context, campaignId.Value, cancellationToken);
            return true;
        }

        var campaignSnapshot = await context.EmailBroadcasts.AsNoTracking()
            .FirstAsync(x => x.Id == campaignId.Value, cancellationToken);
        var attachments = await context.EmailBroadcastAttachments.AsNoTracking()
            .Where(x => x.EmailBroadcastId == campaignId.Value).OrderBy(x => x.FileName)
            .ToListAsync(cancellationToken);

        foreach (var recipientId in claimedIds)
        {
            var recipient = await context.EmailBroadcastRecipients.FirstAsync(x => x.Id == recipientId,
                cancellationToken);
            try
            {
                var providerReference = $"delivery-{Guid.NewGuid():N}";
                await gateway.SendNotificationAsync(IntegrationKind.Email, recipient.Email,
                    "broadcast-email", new
                    {
                        campaignSnapshot.CampaignNumber, campaignSnapshot.Name,
                        campaignSnapshot.Subject, campaignSnapshot.Body, campaignSnapshot.ReplyTo,
                        Recipient = new { recipient.RecipientName, recipient.SourceType, recipient.SourceId },
                        Attachments = attachments.Select(x => new
                            {
                                x.FileName, x.ContentType, x.SizeBytes, x.StorageKey, x.Checksum,
                                ContentBase64 = Convert.ToBase64String(x.Content)
                            })
                    }, cancellationToken);
                recipient.Status = EmailBroadcastRecipientStatus.Sent;
                recipient.SentAt = DateTime.UtcNow;
                recipient.ProviderReference = providerReference;
                if (recipient.SourceType == EmailRecipientSourceType.Client)
                    context.CommunicationMessages.Add(new CommunicationMessage
                    {
                        CustomerId = recipient.SourceId, Type = CommunicationType.General,
                        RecipientEmail = recipient.Email, Subject = campaignSnapshot.Subject,
                        Body = campaignSnapshot.Body, ReferenceType = "EmailBroadcast",
                        ReferenceId = campaignSnapshot.Id, Status = CommunicationStatus.Sent,
                        ProviderReference = providerReference, SentAt = recipient.SentAt
                    });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                recipient.Status = EmailBroadcastRecipientStatus.Failed;
                recipient.FailureReason = exception.Message.Length > 900
                    ? exception.Message[..900] : exception.Message;
                _logger.LogWarning(exception, "Broadcast {CampaignId} failed for recipient {RecipientId}.",
                    campaignId, recipientId);
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        await RefreshCampaignAsync(context, campaignId.Value, cancellationToken);
        return true;
    }

    private static async Task RefreshCampaignAsync(ApplicationDbContext context, Guid campaignId,
        CancellationToken cancellationToken)
    {
        var campaign = await context.EmailBroadcasts.FirstAsync(x => x.Id == campaignId,
            cancellationToken);
        if (campaign.Status == EmailBroadcastStatus.Cancelled) return;
        var counts = await context.EmailBroadcastRecipients.AsNoTracking()
            .Where(x => x.EmailBroadcastId == campaignId).GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(cancellationToken);
        int Count(EmailBroadcastRecipientStatus status) =>
            counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var processing = Count(EmailBroadcastRecipientStatus.Processing);
        campaign.PendingRecipients = Count(EmailBroadcastRecipientStatus.Pending) + processing;
        campaign.SentRecipients = Count(EmailBroadcastRecipientStatus.Sent);
        campaign.FailedRecipients = Count(EmailBroadcastRecipientStatus.Failed);
        campaign.SkippedRecipients = Count(EmailBroadcastRecipientStatus.Skipped);
        if (campaign.PendingRecipients == 0)
        {
            campaign.Status = campaign.FailedRecipients == 0
                ? EmailBroadcastStatus.Sent : EmailBroadcastStatus.PartiallyFailed;
            campaign.CompletedAt = DateTime.UtcNow;
        }
        else campaign.Status = EmailBroadcastStatus.Sending;
        await context.SaveChangesAsync(cancellationToken);
    }
}

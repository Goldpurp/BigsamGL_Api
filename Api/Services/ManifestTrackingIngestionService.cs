using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed record ManifestTrackingIngestionResult(
    int EventsReceived,
    int NewEventsStored,
    int DuplicatesIgnored,
    bool IsMock,
    DateTime SyncedAt,
    IReadOnlyList<ManifestTrackingEvent> NewEvents);

public interface IManifestTrackingIngestionService
{
    Task<ManifestTrackingIngestionResult> SyncAsync(Guid connectionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Imports MSC/DCSA movements as internal drafts. Publication always remains a separate
/// staff-reviewed operation.
/// </summary>
public sealed class ManifestTrackingIngestionService : IManifestTrackingIngestionService
{
    private readonly ApplicationDbContext _context;
    private readonly IMscTrackingGateway _msc;
    private readonly IDataProtector _payloadProtector;

    public ManifestTrackingIngestionService(ApplicationDbContext context, IMscTrackingGateway msc,
        IDataProtectionProvider dataProtection)
    {
        _context = context;
        _msc = msc;
        _payloadProtector = dataProtection.CreateProtector(
            "BiGSAM.ManifestTracking.ProviderPayload.v1");
    }

    public async Task<ManifestTrackingIngestionResult> SyncAsync(Guid connectionId,
        CancellationToken cancellationToken)
    {
        var connection = await _context.ManifestTrackingConnections.FirstOrDefaultAsync(x =>
            x.Id == connectionId, cancellationToken)
            ?? throw new KeyNotFoundException("Manifest tracking connection not found.");
        if (!connection.Provider.Equals("MSC", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Direct pull tracking is currently available only for MSC.");

        var snapshot = await _msc.FetchAsync(connection.ExternalReference,
            connection.ReferenceType, cancellationToken);
        var now = DateTime.UtcNow;
        var lockKey = $"manifest-tracking:{connection.Id:N}";
        var added = new List<ManifestTrackingEvent>();

        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock(hashtext({lockKey}))", cancellationToken);
            var receivedIds = snapshot.Events.Select(x => x.ExternalEventId).Distinct().ToList();
            var existingIds = await _context.ManifestTrackingEvents.AsNoTracking().Where(x =>
                    x.ConnectionId == connection.Id && x.ProviderEventId != null &&
                    receivedIds.Contains(x.ProviderEventId))
                .Select(x => x.ProviderEventId!).ToListAsync(cancellationToken);
            var existing = existingIds.ToHashSet(StringComparer.Ordinal);

            foreach (var carrierEvent in snapshot.Events.Where(x =>
                         !existing.Contains(x.ExternalEventId)))
            {
                var value = new ManifestTrackingEvent
                {
                    BranchId = connection.BranchId,
                    ConnectionId = connection.Id,
                    ProviderEventId = carrierEvent.ExternalEventId,
                    Source = TrackingEventSource.Provider,
                    ReviewState = TrackingEventReviewState.Draft,
                    ProviderStatus = carrierEvent.EventStatus,
                    InternalTitle = TrackingTitle(carrierEvent),
                    InternalDescription = carrierEvent.Description,
                    Place = Null(carrierEvent.Location),
                    OccurredAt = carrierEvent.OccurredAt,
                    EstimatedAt = carrierEvent.EstimatedArrivalAt,
                    RawPayloadProtected = JsonSerializer.Serialize(new
                    {
                        format = "aspnet-data-protection-v1",
                        ciphertext = _payloadProtector.Protect(carrierEvent.RawPayloadJson)
                    })
                };
                added.Add(value);
                _context.ManifestTrackingEvents.Add(value);
            }

            var latestActual = snapshot.Events.LastOrDefault(x =>
                x.EventStatus.Equals("ACT", StringComparison.OrdinalIgnoreCase)) ??
                snapshot.Events.LastOrDefault();
            connection.ProviderStatus = latestActual is null
                ? snapshot.Status
                : $"{latestActual.EventStatus}:{latestActual.EventTypeCode}";
            connection.Status = ManifestStatus(latestActual?.EventTypeCode);
            connection.LastKnownPlace = Null(snapshot.CurrentLocation);
            connection.EstimatedAt = snapshot.EstimatedArrivalAt;
            connection.LastSuccessfulSyncAt = now;
            connection.NextSyncAt = now.AddMinutes(30);
            connection.RowVersion++;
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_unlock(hashtext({lockKey}))", cancellationToken);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        return new ManifestTrackingIngestionResult(snapshot.Events.Count, added.Count,
            snapshot.Events.Count - added.Count, snapshot.IsMock, now, added);
    }

    private static string TrackingTitle(MscTrackingEvent value) =>
        value.EventStatus.ToUpperInvariant() switch
        {
            "EST" => $"MSC estimated {value.EventTypeCode}",
            "PLN" => $"MSC planned {value.EventTypeCode}",
            _ => $"MSC confirmed {value.EventTypeCode}"
        };

    private static ManifestTrackingStatus ManifestStatus(string? code) =>
        code?.Trim().ToUpperInvariant() switch
        {
            "ARRI" or "DISC" => ManifestTrackingStatus.Arrived,
            "DELY" => ManifestTrackingStatus.Delivered,
            _ => ManifestTrackingStatus.InTransit
        };

    private static string? Null(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Periodically imports due MSC movements in live mode without publishing them.</summary>
public sealed class MscTrackingSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MscTrackingOptions _options;
    private readonly ILogger<MscTrackingSyncWorker> _logger;

    public MscTrackingSyncWorker(IServiceScopeFactory scopeFactory,
        IOptions<MscTrackingOptions> options, ILogger<MscTrackingSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(_options.Mode, "Live", StringComparison.OrdinalIgnoreCase) ||
            !_options.AutomaticSyncEnabled)
        {
            _logger.LogInformation(
                "Automatic MSC synchronization is disabled because live mode is not enabled.");
            return;
        }

        var scanInterval = TimeSpan.FromSeconds(
            Math.Clamp(_options.WorkerScanSeconds, 30, 900));
        using var timer = new PeriodicTimer(scanInterval);
        await SynchronizeDueConnectionsAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SynchronizeDueConnectionsAsync(stoppingToken);
    }

    private async Task SynchronizeDueConnectionsAsync(CancellationToken cancellationToken)
    {
        List<Guid> due;
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            due = await context.ManifestTrackingConnections.AsNoTracking()
                .Where(x => x.Provider == "MSC" &&
                    (x.NextSyncAt == null || x.NextSyncAt <= now) &&
                    x.Status != ManifestTrackingStatus.Delivered)
                .OrderBy(x => x.NextSyncAt).ThenBy(x => x.Id)
                .Select(x => x.Id)
                .Take(Math.Clamp(_options.WorkerBatchSize, 1, 100))
                .ToListAsync(cancellationToken);
        }

        foreach (var connectionId in due)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IManifestTrackingIngestionService>();
                var result = await service.SyncAsync(connectionId, cancellationToken);
                _logger.LogInformation(
                    "MSC sync completed for {ConnectionId}: {NewEvents}/{EventsReceived} new drafts.",
                    connectionId, result.NewEventsStored, result.EventsReceived);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Automatic MSC sync failed for {ConnectionId}; a retry has been scheduled.",
                    connectionId);
                await ScheduleRetryAsync(connectionId, cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        }
    }

    private async Task ScheduleRetryAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = await context.ManifestTrackingConnections.FirstOrDefaultAsync(x =>
            x.Id == connectionId, cancellationToken);
        if (connection is null) return;
        connection.NextSyncAt = DateTime.UtcNow.AddMinutes(
            Math.Clamp(_options.FailureRetryMinutes, 5, 240));
        await context.SaveChangesAsync(cancellationToken);
    }
}

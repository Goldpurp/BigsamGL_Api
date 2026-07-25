using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ShipmentTrackingService : IShipmentTrackingService
{
    private readonly IApplicationDbContext _context;
    private readonly IClientCommunicationService _communications;

    public ShipmentTrackingService(IApplicationDbContext context, IClientCommunicationService communications)
    {
        _context = context;
        _communications = communications;
    }

    public async Task<ShipmentMilestone> RecordAsync(Guid cargoId, ShipmentTrackingInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        if (!string.IsNullOrWhiteSpace(input.ExternalEventId))
        {
            var existing = await _context.ShipmentMilestones.FirstOrDefaultAsync(x =>
                x.CargoItemId == cargoId && x.CarrierProvider == input.CarrierProvider && x.ExternalEventId == input.ExternalEventId,
                cancellationToken);
            if (existing is not null) return existing;
        }

        var cargo = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == cargoId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo not found.");
        cargo.UpdateStatus(input.Status);
        if (!string.IsNullOrWhiteSpace(input.CarrierProvider))
        {
            cargo.CarrierProvider = input.CarrierProvider.Trim();
            cargo.LastCarrierSyncAt = DateTime.UtcNow;
        }

        var milestone = CreateMilestone(cargoId, input);
        _context.ShipmentMilestones.Add(milestone);
        await _context.SaveChangesAsync(cancellationToken);

        if (input.NotifyCustomer && input.Visibility == MilestoneVisibility.CustomerVisible &&
            await CanNotifyAsync(cargo.CustomerId, cancellationToken))
        {
            await NotifyAsync(cargo, input.Title, input.Description, milestone.Id, new
            {
                cargo.TrackingNumber, cargo.MarkerCode, input.Status, input.EventType, input.Location,
                input.OccurredAt, input.EstimatedDeliveryAt, input.IsException, input.IssueCode
            }, cancellationToken);
            milestone.CustomerNotifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return milestone;
    }

    public async Task<ShipmentMilestone> CorrectAsync(Guid cargoId, Guid milestoneId,
        ShipmentTrackingInput input, string revisionReason, CancellationToken cancellationToken)
    {
        Validate(input);
        if (string.IsNullOrWhiteSpace(revisionReason) || revisionReason.Trim().Length < 5)
            throw new ArgumentException("A correction reason of at least 5 characters is required.");
        var cargo = await _context.CargoItems.FirstOrDefaultAsync(x => x.Id == cargoId, cancellationToken)
            ?? throw new KeyNotFoundException("Cargo not found.");
        var milestone = await _context.ShipmentMilestones.FirstOrDefaultAsync(x =>
            x.Id == milestoneId && x.CargoItemId == cargoId, cancellationToken)
            ?? throw new KeyNotFoundException("Tracking event not found.");

        milestone.Status = input.Status;
        milestone.EventType = input.EventType;
        milestone.Title = input.Title.Trim();
        milestone.Description = input.Description.Trim();
        milestone.Location = input.Location.Trim();
        milestone.OccurredAt = input.OccurredAt.ToUniversalTime();
        milestone.Visibility = input.Visibility;
        milestone.EstimatedDeliveryAt = input.EstimatedDeliveryAt?.ToUniversalTime();
        milestone.IsException = input.IsException;
        milestone.IssueCode = input.IssueCode?.Trim();
        milestone.IsCorrected = true;
        milestone.RevisionReason = revisionReason.Trim();

        var newerExists = await _context.ShipmentMilestones.AnyAsync(x =>
            x.CargoItemId == cargoId && x.Id != milestoneId && x.OccurredAt > milestone.OccurredAt,
            cancellationToken);
        if (!newerExists) cargo.UpdateStatus(input.Status);
        await _context.SaveChangesAsync(cancellationToken);

        if (input.NotifyCustomer && input.Visibility == MilestoneVisibility.CustomerVisible &&
            await CanNotifyAsync(cargo.CustomerId, cancellationToken))
        {
            await NotifyAsync(cargo, $"Tracking correction: {input.Title}", input.Description,
                milestone.Id, new { cargo.TrackingNumber, input.Status, input.EventType, input.Location,
                    input.OccurredAt, RevisionReason = revisionReason }, cancellationToken);
            milestone.CustomerNotifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return milestone;
    }

    public async Task<BatchTrackingResult> RecordBatchAsync(Guid batchId, ShipmentTrackingInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        var batch = await _context.ShippingBatches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Shipment batch not found.");
        var cargoItems = await _context.CargoItems.Where(x => x.BatchId == batchId).ToListAsync(cancellationToken);
        if (cargoItems.Count == 0) throw new InvalidOperationException("The shipment batch has no cargo items.");

        var milestones = new List<ShipmentMilestone>(cargoItems.Count);
        foreach (var cargo in cargoItems)
        {
            cargo.UpdateStatus(input.Status);
            var milestone = CreateMilestone(cargo.Id, input);
            milestones.Add(milestone);
            _context.ShipmentMilestones.Add(milestone);
        }
        if (input.EventType is ShipmentEventType.Delayed or ShipmentEventType.Issue) batch.Status = BatchStatus.Delayed;
        await _context.SaveChangesAsync(cancellationToken);

        var notified = 0;
        if (input.NotifyCustomer && input.Visibility == MilestoneVisibility.CustomerVisible)
        {
            foreach (var customerGroup in cargoItems.GroupBy(x => x.CustomerId))
            {
                if (!await CanNotifyAsync(customerGroup.Key, cancellationToken)) continue;
                var representative = customerGroup.First();
                await NotifyAsync(representative, input.Title, input.Description, batchId, new
                {
                    batch.BatchCode, input.Status, input.EventType, input.Location, input.OccurredAt,
                    input.EstimatedDeliveryAt, input.IsException, input.IssueCode,
                    TrackingNumbers = customerGroup.Select(x => x.TrackingNumber).ToList()
                }, cancellationToken, "ShipmentBatch");
                var notifiedAt = DateTime.UtcNow;
                var cargoIds = customerGroup.Select(x => x.Id).ToHashSet();
                foreach (var milestone in milestones.Where(x => cargoIds.Contains(x.CargoItemId)))
                    milestone.CustomerNotifiedAt = notifiedAt;
                notified++;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new BatchTrackingResult(batchId, cargoItems.Count, notified, milestones.Select(x => x.Id).ToList());
    }

    private async Task<bool> CanNotifyAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var preference = await _context.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
        return preference is null || preference.EmailEnabled && preference.OperationalUpdates;
    }

    private Task NotifyAsync(CargoItem cargo, string subject, string body, Guid referenceId,
        object payload, CancellationToken cancellationToken, string referenceType = "CargoItem") =>
        _communications.SendAsync(cargo.CustomerId, CommunicationType.TrackingUpdate,
            subject, body, referenceType, referenceId, Array.Empty<Guid>(), payload, cancellationToken);

    private static ShipmentMilestone CreateMilestone(Guid cargoId, ShipmentTrackingInput input) => new()
    {
        CargoItemId = cargoId,
        Status = input.Status,
        EventType = input.EventType,
        Title = input.Title.Trim(),
        Description = input.Description.Trim(),
        Location = input.Location.Trim(),
        OccurredAt = input.OccurredAt.ToUniversalTime(),
        Visibility = input.Visibility,
        Source = input.Source.Trim(),
        CarrierProvider = input.CarrierProvider?.Trim(),
        ExternalEventId = input.ExternalEventId?.Trim(),
        IsException = input.IsException,
        IssueCode = input.IssueCode?.Trim(),
        EstimatedDeliveryAt = input.EstimatedDeliveryAt?.ToUniversalTime()
    };

    private static void Validate(ShipmentTrackingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 160)
            throw new ArgumentException("Tracking title is required and cannot exceed 160 characters.");
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Trim().Length > 500)
            throw new ArgumentException("Tracking description is required and cannot exceed 500 characters.");
        if (input.OccurredAt == default || input.OccurredAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(10))
            throw new ArgumentException("Tracking event time is invalid or too far in the future.");
        if (input.IsException && string.IsNullOrWhiteSpace(input.IssueCode))
            throw new ArgumentException("An issue code is required for exception events.");
    }
}

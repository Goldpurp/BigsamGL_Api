using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ShippingBatch : BaseEntity
{
    public string BatchCode { get; set; } = string.Empty;
    public string? AirwayBillNumber { get; set; }
    public decimal MaxWeightCapacityKg { get; set; }
    public DateTime ScheduledReleaseDate { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Draft;
    public string OriginHub { get; set; } = string.Empty;
    public string DestinationHub { get; set; } = string.Empty;
    public ConsolidationType ConsolidationType { get; set; } = ConsolidationType.Groupage;
    // Legacy carrier display fields are retained for API/data compatibility. New carrier
    // details belong to ShipmentMovement, because BigsamGL arranges rather than operates
    // the main air or sea transport.
    public string VesselName { get; set; } = string.Empty;
    public string VoyageNumber { get; set; } = string.Empty;
    public string PortOfLoading { get; set; } = string.Empty;
    public string PortOfDischarge { get; set; } = string.Empty;
    public bool FreezeTracking { get; set; }

    public ShippingBatch()
    {
    }

    public ShippingBatch(
        Guid id,
        string batchCode,
        string? airwayBillNumber,
        decimal maxWeightCapacityKg,
        DateTime scheduledReleaseDate,
        string originHub,
        string destinationHub)
    {
        Id = id;
        BatchCode = batchCode;
        AirwayBillNumber = airwayBillNumber;
        MaxWeightCapacityKg = maxWeightCapacityKg;
        ScheduledReleaseDate = scheduledReleaseDate;
        Status = BatchStatus.Draft;
        OriginHub = originHub;
        DestinationHub = destinationHub;
        CreatedAt = DateTime.UtcNow;
    }

    public bool CanAddCargoItem()
    {
        return Status == BatchStatus.Draft || Status == BatchStatus.Locked;
    }

    public void AddCargoItem(CargoItem cargoItem, decimal currentTotalWeightKg)
    {
        if (!CanAddCargoItem())
        {
            throw new InvalidOperationException($"Cannot add items to batch in {Status} status.");
        }

        if (currentTotalWeightKg + cargoItem.BillableWeightKg > MaxWeightCapacityKg)
        {
            throw new InvalidOperationException($"Payload conflict: Adding cargo exceeding batch weight limits. Max capacity: {MaxWeightCapacityKg} kg, current cumulative weight: {currentTotalWeightKg} kg, item weight: {cargoItem.BillableWeightKg} kg");
        }
    }

    public void LockBatch()
    {
        if (Status != BatchStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft batches can transition to Locked status.");
        }
        Status = BatchStatus.Locked;
    }

    public void DispatchBatch(string billNumber)
    {
        if (string.IsNullOrWhiteSpace(billNumber))
        {
            throw new ArgumentException("Dispatch requires a valid bill number.");
        }
        if (Status != BatchStatus.Locked && Status != BatchStatus.Draft)
        {
            throw new InvalidOperationException("Batch must be Locked or Draft to dispatch.");
        }
        AirwayBillNumber = billNumber;
        Status = BatchStatus.Dispatched;
    }

    public void ArriveAtCustoms()
    {
        if (Status != BatchStatus.Dispatched)
        {
            throw new InvalidOperationException("Batch must be Dispatched to arrive at customs.");
        }
        Status = BatchStatus.ArrivedAtCustoms;
    }

    public void ReleaseFromCustoms()
    {
        if (Status != BatchStatus.ArrivedAtCustoms)
        {
            throw new InvalidOperationException("Batch must be at customs to release.");
        }
        Status = BatchStatus.Released;
    }

    public void DelayBatch()
    {
        Status = BatchStatus.Delayed;
    }

    public void RescheduleRelease(DateTime newDate)
    {
        if (newDate <= DateTime.UtcNow)
        {
            throw new ArgumentException("Rescheduled date must occupy a future calendar timestamp.");
        }
        ScheduledReleaseDate = newDate;
    }
}

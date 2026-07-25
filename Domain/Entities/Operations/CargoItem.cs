using Domain.Common;
using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Entities;

public class CargoItem : BaseEntity
{
    public Guid CustomerId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string MarkerCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ActualWeightKg { get; set; }
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal VolumetricWeightKg { get; set; }
    public decimal DeclaredValueAmount { get; set; }
    public string DeclaredValueCurrency { get; set; } = "USD";
    public string CustomsCategory { get; set; } = "General Cargo";
    public CargoStatus Status { get; set; } = CargoStatus.Logged;
    public decimal ShippingCostAmount { get; set; }
    public string ShippingCostCurrency { get; set; } = "USD";
    public Guid? BatchId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? ShipmentMovementId { get; set; }
    public Guid? ShipmentContainerId { get; set; }
    public TransportMode? AssignedTransportMode { get; set; }
    public string? CarrierProvider { get; set; }
    public string? ExternalTrackingNumber { get; set; }
    public DateTime? LastCarrierSyncAt { get; set; }

    public CargoItem()
    {
    }

    public CargoItem(
        Guid id,
        Guid customerId,
        string trackingNumber,
        string description,
        decimal actualWeightKg,
        decimal lengthCm,
        decimal widthCm,
        decimal heightCm,
        decimal declaredValueAmount,
        string declaredValueCurrency,
        string customsCategory,
        string countryCode)
    {
        Id = id;
        CustomerId = customerId;
        TrackingNumber = trackingNumber;
        Description = description;
        ActualWeightKg = actualWeightKg;
        LengthCm = lengthCm;
        WidthCm = widthCm;
        HeightCm = heightCm;
        DeclaredValueAmount = declaredValueAmount;
        DeclaredValueCurrency = declaredValueCurrency.ToUpperInvariant();
        CustomsCategory = customsCategory;
        Status = CargoStatus.Logged;
        ShippingCostCurrency = DeclaredValueCurrency;
        CreatedAt = DateTime.UtcNow;

        // Volumetric weight calculation
        var vol = new VolumetricWeight(lengthCm, widthCm, heightCm);
        VolumetricWeightKg = vol.WeightKg;

        // SGMC Code: Destination-MMDD-ShortUnique
        string dateStr = CreatedAt.ToString("MMdd");
        string shortUnique = Id.ToString().Split('-').Last().Substring(0, 2).ToUpperInvariant();
        MarkerCode = $"{countryCode.ToUpperInvariant()}-{dateStr}-{shortUnique}";
    }

    public decimal BillableWeightKg => Math.Max(ActualWeightKg, VolumetricWeightKg);

    public decimal CalculateShippingRates(decimal ratePerKg, decimal customsTaxesMultiplier)
    {
        decimal baseCost = BillableWeightKg * ratePerKg;
        decimal taxCost = DeclaredValueAmount * customsTaxesMultiplier;
        return Math.Round(baseCost + taxCost, 2);
    }

    public void AssignToBatch(Guid batchId)
    {
        if (Status != CargoStatus.Logged && Status != CargoStatus.ReceivedAtWarehouse)
        {
            throw new InvalidOperationException($"Cannot batch cargo item with status: {Status}");
        }
        BatchId = batchId;
        Status = CargoStatus.AssignedToBatch;
    }

    public void RemoveFromBatch()
    {
        BatchId = null;
        Status = CargoStatus.ReceivedAtWarehouse;
    }

    public void UpdateStatus(CargoStatus newStatus)
    {
        Status = newStatus;
    }
}

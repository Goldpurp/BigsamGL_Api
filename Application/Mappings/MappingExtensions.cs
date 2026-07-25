using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class MappingExtensions
{
    public static CustomerDto ToDto(this Customer entity)
    {
        return new CustomerDto(
            entity.Id,
            entity.FullName,
            entity.Email ?? string.Empty,
            entity.WalletBalance,
            entity.WalletCurrency,
            entity.CountryCode,
            entity.Phone,
            entity.CompanyName,
            entity.IsBusiness,
            entity.CreatedAt,
            entity.AccountType
        );
    }

    public static CargoItemDto ToDto(this CargoItem entity)
    {
        return new CargoItemDto(
            entity.Id,
            entity.CustomerId,
            entity.TrackingNumber,
            entity.MarkerCode,
            entity.Description,
            entity.ActualWeightKg,
            entity.LengthCm,
            entity.WidthCm,
            entity.HeightCm,
            entity.VolumetricWeightKg,
            entity.BillableWeightKg,
            entity.DeclaredValueAmount,
            entity.DeclaredValueCurrency,
            entity.CustomsCategory,
            entity.Status,
            entity.ShippingCostAmount,
            entity.ShippingCostCurrency,
            entity.BatchId,
            entity.InvoiceId,
            entity.ShipmentMovementId,
            entity.ShipmentContainerId,
            entity.AssignedTransportMode,
            entity.CreatedAt
        );
    }

    public static ShippingBatchDto ToDto(this ShippingBatch entity)
    {
        return new ShippingBatchDto(
            entity.Id,
            entity.BatchCode,
            entity.AirwayBillNumber,
            entity.MaxWeightCapacityKg,
            entity.ScheduledReleaseDate,
            entity.Status,
            entity.OriginHub,
            entity.DestinationHub,
            entity.ConsolidationType,
            entity.VesselName,
            entity.VoyageNumber,
            entity.PortOfLoading,
            entity.PortOfDischarge,
            entity.FreezeTracking,
            entity.CreatedAt
        );
    }

    public static WalletLedgerDto ToDto(this WalletLedger entity)
    {
        return new WalletLedgerDto(
            entity.Id,
            entity.CustomerId,
            entity.ReferenceId,
            entity.Amount,
            entity.Currency,
            entity.TransactionType,
            entity.Description,
            entity.Timestamp
        );
    }
}

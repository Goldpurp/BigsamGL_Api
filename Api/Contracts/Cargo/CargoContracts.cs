using Domain.Enums;

namespace Api.Controllers;

public record CreateBatchRequest(
    string BatchCode,
    string? AirwayBillNumber,
    decimal MaxWeightCapacityKg,
    DateTime ScheduledReleaseDate,
    string OriginHub,
    string DestinationHub,
    string? VesselName,
    string? VoyageNumber,
    string? PortOfLoading,
    string? PortOfDischarge,
    ConsolidationType ConsolidationType = ConsolidationType.Groupage);

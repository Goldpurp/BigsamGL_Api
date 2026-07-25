namespace Domain.Enums;

/// <summary>Separates day-to-day manifest records and reporting without mixing air and ship work.</summary>
public enum ManifestLogisticsMethod
{
    Air = 0,
    Ship = 1
}

/// <summary>
/// Describes how one package on a manifest is handed to its owner. This is intentionally
/// separate from booking delivery so manifest screens can use the company's familiar labels.
/// </summary>
public enum ManifestHandoverMethod
{
    HubCollection = 0,
    SentOut = 1
}

/// <summary>
/// Records the commercial rule used to price one customer shipment. Standard Air charges are
/// calculated from actual/volumetric weight, standard Sea charges from boxes/items, while
/// negotiated pricing preserves exceptional machinery and non-catalogue agreements.
/// </summary>
public enum ShipmentChargeBasis
{
    AirWeightAndDimensions = 0,
    SeaBoxesAndItems = 1,
    Negotiated = 2
}

/// <summary>Separates immutable shipment charge lines without relying on description text.</summary>
public enum ShipmentChargeLineType
{
    AirWeight = 0,
    SeaBox = 1,
    SeaItem = 2,
    Negotiated = 3
}

namespace Domain.Enums;

public enum TransportMode { Air = 0, Sea = 1, Road = 2, Courier = 3 }
public enum ShipmentMovementStatus { Planning = 0, OpenForAllocation = 1, Closed = 2, Departed = 3, Arrived = 4, Cancelled = 5 }
public enum TransportOperatorType
{
    ThirdPartyCarrier = 0,
    PartnerForwarder = 1,
    BigsamLocalDelivery = 2
}
public enum ConsolidationType
{
    Groupage = 0,
    DedicatedClientLoad = 1,
    PartnerAgencyLoad = 2,
    MixedDirectAndAgency = 3
}
public enum ContainerType
{
    NotSpecified = 0,
    Dry20Ft = 1,
    Dry40Ft = 2,
    HighCube40Ft = 3,
    Refrigerated20Ft = 4,
    Refrigerated40Ft = 5,
    OpenTop = 6,
    FlatRack = 7,
    Tank = 8,
    SharedLcl = 9,
    Other = 10
}
public enum LegacyImportStatus { Previewed = 0, Committing = 1, Completed = 2, CompletedWithErrors = 3, Failed = 4 }
public enum LegacyImportRowStatus { Valid = 0, Duplicate = 1, Error = 2, Imported = 3, Skipped = 4 }
public enum LegacyImportEntityType { Client = 0, Invoice = 1, Cargo = 2 }
public enum AddressType { Billing = 0, Collection = 1, Delivery = 2, Warehouse = 3 }
public enum DocumentType { CommercialInvoice = 0, PackingList = 1, IdentityDocument = 2, CustomsDeclaration = 3, ProofOfDelivery = 4, CargoPhoto = 5, Other = 6 }
public enum MilestoneVisibility { InternalOnly = 0, CustomerVisible = 1 }
public enum DeliveryMethod { HubPickup = 0, AddressDelivery = 1 }
public enum ShipmentEventType
{
    General = 0,
    PickupScheduled = 1,
    PickedUp = 2,
    WarehouseReceived = 3,
    WarehouseMoved = 4,
    DepartedOrigin = 5,
    InTransit = 6,
    ArrivedAtHub = 7,
    Customs = 8,
    OutForDelivery = 9,
    Delivered = 10,
    Delayed = 11,
    Issue = 12,
    Resolved = 13,
    ManualCorrection = 14,
    CarrierUpdate = 15
}

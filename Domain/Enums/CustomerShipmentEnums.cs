namespace Domain.Enums;

public enum CustomerShipmentMethod { Air = 0, Sea = 1 }
public enum CustomerShipmentPricingMethod { Weight = 0, BoxSizesAndItems = 1, NegotiatedPrice = 2 }
public enum ShipmentHandoverMethod { Pickup = 0, Delivery = 1 }
public enum ShipmentGoodsType { Fragile = 0, NonFragile = 1 }
public enum CustomerShipmentMovementState
{
    New = 0,
    ReadyForManifest = 1,
    AssignedToManifest = 2,
    OnTheWay = 3,
    Delivered = 4,
    Cancelled = 5
}
public enum ShipmentPriceComponentType { Weight = 0, CatalogueBox = 1, CatalogueItem = 2, Negotiated = 3 }
public enum FreightClassification { Air = 0, Sea = 1, Other = 2 }

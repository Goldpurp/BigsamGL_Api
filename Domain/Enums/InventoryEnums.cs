namespace Domain.Enums;

public enum InventoryItemType
{
    PackagingMaterial = 0,
    OfficeSupply = 1,
    SparePart = 2,
    Consumable = 3,
    Equipment = 4,
    ResaleItem = 5,
    Other = 6
}

public enum InventoryLocationType
{
    Warehouse = 0,
    Office = 1,
    Vehicle = 2,
    Transit = 3,
    Quarantine = 4,
    Other = 5
}

public enum InventoryTransactionType
{
    OpeningBalance = 0,
    PurchaseReceipt = 1,
    Transfer = 2,
    Issue = 3,
    CustomerReturn = 4,
    AdjustmentIncrease = 5,
    AdjustmentDecrease = 6,
    WriteOff = 7,
    StockCountCorrection = 8
}

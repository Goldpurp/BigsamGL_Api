using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class InventoryLocation : BaseEntity
{
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InventoryLocationType Type { get; set; }
    public string AddressOrArea { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

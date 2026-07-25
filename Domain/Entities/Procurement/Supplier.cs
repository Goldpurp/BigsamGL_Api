using Domain.Common;

namespace Domain.Entities;

public class Supplier : BaseEntity
{
    public string SupplierNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

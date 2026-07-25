using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class CustomerAddress : BaseEntity
{
    public Guid CustomerId { get; set; }
    public AddressType Type { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

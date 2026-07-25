using Domain.Common;

namespace Domain.Entities;

public class Branch : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

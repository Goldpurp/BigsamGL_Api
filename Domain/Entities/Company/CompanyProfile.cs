using Domain.Common;

namespace Domain.Entities;

public class CompanyProfile : BaseEntity
{
    public string LegalName { get; set; } = "BiGSAM Global Logistics Limited";
    public string TradingName { get; set; } = "BiGSAM Global Logistics";
    public string RegistrationNumber { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Website { get; set; } = "https://bigsamgl.com";
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string RegisteredAddress { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "GBP";
    public string TimeZone { get; set; } = "Europe/London";
    public string? LogoStorageKey { get; set; }
}

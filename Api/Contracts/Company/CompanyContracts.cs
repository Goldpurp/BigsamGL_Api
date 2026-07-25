namespace Api.Controllers;

public record UpdateCompanyProfileRequest(string LegalName, string TradingName, string RegistrationNumber, string TaxNumber,
    string Website, string Email, string Phone, string RegisteredAddress, string BaseCurrency, string TimeZone, string? LogoStorageKey);
public record UpdateBranchControlRequest(bool IsBookingEnabled, bool IsReceivingEnabled,
    bool IsDispatchEnabled,
    decimal CapacityAlertPercent, int? DailyCargoLimit, string? EmergencyNotice);

using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ClientProfile : BaseEntity
{
    public Guid CustomerId { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Active;
    public Guid? AccountManagerUserId { get; set; }
    public Guid? PreferredBranchId { get; set; }
    public string TaxNumber { get; set; } = string.Empty;
    public string BusinessRegistrationNumber { get; set; } = string.Empty;
    public string BillingCurrency { get; set; } = "GBP";
    public decimal CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 14;
    public string? InternalNotes { get; set; }
    public string? RiskNotes { get; set; }
}

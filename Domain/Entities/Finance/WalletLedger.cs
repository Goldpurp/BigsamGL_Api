using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class WalletLedger : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid ReferenceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public LedgerTransactionType TransactionType { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public WalletLedger()
    {
    }

    public WalletLedger(Guid id, Guid customerId, Guid referenceId, decimal amount, string currency, LedgerTransactionType transactionType, string description)
    {
        Id = id;
        CustomerId = customerId;
        ReferenceId = referenceId;
        Amount = amount;
        Currency = currency.ToUpperInvariant();
        TransactionType = transactionType;
        Description = description;
        Timestamp = DateTime.UtcNow;
        CreatedAt = Timestamp;
    }
}

using Domain.Common;

namespace Domain.Entities;

public class JournalLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal ExchangeRate { get; set; } = 1;
}

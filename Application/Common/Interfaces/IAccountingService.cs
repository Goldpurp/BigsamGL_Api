using Domain.Entities;

namespace Application.Common.Interfaces;

public record JournalPostingLine(string AccountCode, decimal Debit, decimal Credit, string Description);

public interface IAccountingService
{
    Task<JournalEntry> PrepareJournalAsync(string sourceType, Guid? sourceId, string description,
        DateTime entryDate, string currency, IReadOnlyCollection<JournalPostingLine> lines,
        CancellationToken cancellationToken, Guid? branchId = null);
}

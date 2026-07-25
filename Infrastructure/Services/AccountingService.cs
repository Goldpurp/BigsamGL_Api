using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class AccountingService : IAccountingService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AccountingService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<JournalEntry> PrepareJournalAsync(string sourceType, Guid? sourceId, string description,
        DateTime entryDate, string currency, IReadOnlyCollection<JournalPostingLine> lines,
        CancellationToken cancellationToken, Guid? branchId = null)
    {
        if (lines.Count < 2) throw new ArgumentException("A journal requires at least two lines.");
        if (lines.Any(x => x.Debit < 0 || x.Credit < 0 || (x.Debit > 0 && x.Credit > 0) || (x.Debit == 0 && x.Credit == 0)))
            throw new ArgumentException("Every journal line must contain either a positive debit or a positive credit.");
        var debit = lines.Sum(x => x.Debit);
        var credit = lines.Sum(x => x.Credit);
        if (debit != credit) throw new ArgumentException($"Journal is not balanced. Debits {debit:F2}; credits {credit:F2}.");

        var codes = lines.Select(x => x.AccountCode).Distinct().ToList();
        var accounts = await _context.LedgerAccounts.Where(x => codes.Contains(x.Code) && x.IsActive)
            .ToDictionaryAsync(x => x.Code, cancellationToken);
        var missing = codes.Where(code => !accounts.ContainsKey(code)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException($"Ledger account(s) not configured: {string.Join(", ", missing)}.");
        var currencyCode = currency.Trim().ToUpperInvariant();
        if (!branchId.HasValue)
        {
            var branches = await _context.Branches.AsNoTracking().Where(x => x.IsActive)
                .ToListAsync(cancellationToken);
            var candidates = branches.Where(x =>
                Application.Common.CountryCurrency.ForCountry(x.CountryCode) == currencyCode).ToList();
            if (candidates.Count == 1) branchId = candidates[0].Id;
        }
        if (branchId.HasValue)
        {
            var period = entryDate.ToUniversalTime().ToString("yyyy-MM");
            if (await _context.AccountingPeriods.AsNoTracking().AnyAsync(x =>
                x.BranchId == branchId && x.Currency == currencyCode && x.Period == period &&
                x.IsClosed, cancellationToken))
                throw new InvalidOperationException(
                    $"Accounting period {period} is closed for {currencyCode}.");
        }

        var entry = new JournalEntry
        {
            BranchId = branchId,
            Currency = currencyCode,
            JournalNumber = $"BGL-JRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            EntryDate = entryDate,
            Description = description.Trim(),
            SourceType = sourceType.Trim(),
            SourceId = sourceId,
            Status = JournalStatus.Posted,
            PostedAt = DateTime.UtcNow,
            PostedByUserId = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null
        };
        _context.JournalEntries.Add(entry);
        foreach (var line in lines)
            _context.JournalLines.Add(new JournalLine
            {
                JournalEntryId = entry.Id,
                LedgerAccountId = accounts[line.AccountCode].Id,
                Description = line.Description.Trim(),
                Debit = line.Debit,
                Credit = line.Credit,
                Currency = currencyCode,
                ExchangeRate = 1
            });
        return entry;
    }
}

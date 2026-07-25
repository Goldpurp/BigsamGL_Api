using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string NormalizedFullName { get; set; } = string.Empty;
    public string ClientNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public decimal WalletBalance { get; set; }
    public string WalletCurrency { get; set; } = "USD";
    public string CountryCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? NormalizedPhone { get; set; }
    public string? CompanyName { get; set; }
    public ClientAccountType AccountType { get; set; } = ClientAccountType.Individual;
    public bool IsBusiness { get; set; }
    public Guid? MergedIntoCustomerId { get; set; }
    public DateTime? MergedAt { get; set; }

    public Customer() { }

    public Customer(Guid id, string fullName, string? email, decimal walletBalance, string countryCode)
    {
        Id = id; FullName = fullName; Email = email; WalletBalance = walletBalance;
        CountryCode = countryCode.ToUpperInvariant(); CreatedAt = DateTime.UtcNow;
    }

    public void CreditWallet(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Credit amount must be positive.");
        WalletBalance += amount;
    }

    public void DebitWallet(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Debit amount must be positive.");
        if (WalletBalance < amount)
            throw new InvalidOperationException($"Insufficient wallet reserves. Balance: {WalletBalance}, Required: {amount}");
        WalletBalance -= amount;
    }
}

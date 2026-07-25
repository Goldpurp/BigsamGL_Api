using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

public class AccountingServiceTests
{
    [Fact]
    public async Task BalancedJournalCreatesPostedEntryAndLines()
    {
        await using var context = CreateContext();
        context.LedgerAccounts.AddRange(
            new LedgerAccount { Code = "1000", Name = "Cash", Type = AccountType.Asset },
            new LedgerAccount { Code = "4000", Name = "Revenue", Type = AccountType.Revenue });
        await context.SaveChangesAsync();
        var service = new AccountingService(context, new TestCurrentUser());

        var entry = await service.PrepareJournalAsync("Test", null, "Balanced entry", DateTime.UtcNow, "GBP",
            new[] { new JournalPostingLine("1000", 100, 0, "Cash"), new JournalPostingLine("4000", 0, 100, "Revenue") }, default);
        await context.SaveChangesAsync();

        Assert.Equal(JournalStatus.Posted, entry.Status);
        Assert.Equal(2, context.JournalLines.Count());
        Assert.Equal(context.JournalLines.Sum(x => x.Debit), context.JournalLines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task UnbalancedJournalIsRejected()
    {
        await using var context = CreateContext();
        var service = new AccountingService(context, new TestCurrentUser());
        await Assert.ThrowsAsync<ArgumentException>(() => service.PrepareJournalAsync("Test", null, "Bad entry",
            DateTime.UtcNow, "GBP", new[]
            {
                new JournalPostingLine("1000", 100, 0, "Cash"),
                new JournalPostingLine("4000", 0, 90, "Revenue")
            }, default));
    }

    [Fact]
    public async Task LocalGatewayPersistsAnExplicitIntegrationRecord()
    {
        await using var context = CreateContext(); var gateway = new LocalIntegrationGateway(context);
        var session = await gateway.CreatePaymentSessionAsync("BOOK-1", 50, "GBP", default);
        await context.SaveChangesAsync();
        Assert.StartsWith("https://local-payments.bigsamgl.test", session.CheckoutUrl);
        Assert.Equal(IntegrationKind.Payment, Assert.Single(context.IntegrationMessages).Kind);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new TestCurrentUser());

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "11111111-1111-1111-1111-111111111111";
        public string? IpAddress => "127.0.0.1";
    }
}

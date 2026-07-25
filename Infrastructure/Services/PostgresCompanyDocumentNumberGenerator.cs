using System.Data;
using Application.Common.Interfaces;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Services;

public sealed class PostgresCompanyDocumentNumberGenerator : ICompanyDocumentNumberGenerator
{
    private readonly ApplicationDbContext _context;
    public PostgresCompanyDocumentNumberGenerator(ApplicationDbContext context) => _context = context;

    public async Task<string> NextClientAsync(CancellationToken cancellationToken) =>
        string.Concat("ID-", (await NextAsync("client_number_seq", cancellationToken)).ToString("D2"));

    public async Task<string> NextTransactionAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("financial_transaction_number_seq", cancellationToken):D8}TXN";

    public async Task<string> NextCreditNoteAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("credit_note_number_seq", cancellationToken):D6}CRN";

    public async Task<string> NextFinancialDocumentAsync(FinancialDocumentType type,
        CancellationToken cancellationToken)
    {
        var number = await NextAsync(type == FinancialDocumentType.Receipt
            ? "receipt_number_seq" : "payment_voucher_number_seq", cancellationToken);
        return type == FinancialDocumentType.Receipt ? $"BGL{number:D6}RCT" : $"BGL{number:D6}PV";
    }

    public async Task<string> NextPurchaseAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("company_purchase_number_seq", cancellationToken):D6}PUR";

    public async Task<string> NextPayoutAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("purchase_payout_number_seq", cancellationToken):D6}PAY";

    public async Task<string> NextInventoryTransactionAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("inventory_transaction_number_seq", cancellationToken):D8}STK";

    public async Task<string> NextAppointmentAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("appointment_number_seq", cancellationToken):D8}APT";

    public async Task<string> NextEmailBroadcastAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("email_broadcast_number_seq", cancellationToken):D8}EML";

    public async Task<string> NextOperationalManifestAsync(CancellationToken cancellationToken) =>
        $"BGL{await NextAsync("operational_manifest_number_seq", cancellationToken):D8}MNF";

    public async Task<string> NextScheduledPickupAsync(CancellationToken cancellationToken) =>
        $"PKU-{await NextAsync("scheduled_pickup_number_seq", cancellationToken):D5}";

    public async Task<string> NextCargoItemIssueAsync(CancellationToken cancellationToken) =>
        $"ISS-{await NextAsync("cargo_item_issue_number_seq", cancellationToken):D6}";

    private async Task<long> NextAsync(string sequence, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"SELECT nextval('{sequence}')";
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}

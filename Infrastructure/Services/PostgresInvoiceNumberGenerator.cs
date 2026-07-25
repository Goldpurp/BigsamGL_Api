using System.Data;
using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Services;

public sealed class PostgresInvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly ApplicationDbContext _context;
    public PostgresInvoiceNumberGenerator(ApplicationDbContext context) => _context = context;

    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT nextval('invoice_number_seq')";
            var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return $"BGL{value:D4}INV";
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}

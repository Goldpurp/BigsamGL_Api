using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public sealed class ShipmentInvoiceService : IShipmentInvoiceService
{
    private readonly IInvoiceNumberGenerator _numbers;

    public ShipmentInvoiceService(IInvoiceNumberGenerator numbers) => _numbers = numbers;

    public async Task<ShipmentInvoiceCreation> CreateAsync(CreateShipmentInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Amount <= 0) throw new ArgumentException("Invoice amount must be greater than zero.");
        if (!PaymentRouting.IsSupported(command.PaymentMethod))
            throw new ArgumentException("Payment method must be Cash, BankTransfer, or Card.");
        if (command.Lines is null || command.Lines.Count == 0 || command.Lines.Any(x =>
                string.IsNullOrWhiteSpace(x.Description) || x.Quantity <= 0 || x.UnitPrice <= 0))
            throw new ArgumentException("At least one valid shipment charge line is required.");
        var lineTotal = command.Lines.Sum(x => Math.Round(x.Quantity * x.UnitPrice, 2));
        if (Math.Abs(lineTotal - command.Amount) > 0.01m)
            throw new ArgumentException("Invoice amount must match the shipment pricing breakdown.");

        var issueDate = DateTime.UtcNow;
        var invoice = new Invoice
        {
            CustomerId = command.CustomerId,
            CustomerName = command.CustomerName.Trim(),
            InvoiceNumber = await _numbers.NextAsync(cancellationToken),
            Type = InvoiceType.Sales,
            IssuerBranchCode = command.IssuerBranchCode.Trim().ToUpperInvariant(),
            IssueDate = issueDate,
            DueDate = issueDate,
            Status = InvoiceStatus.Issued,
            PaymentStatus = InvoicePaymentStatus.Unpaid,
            PreferredPaymentMethod = command.PaymentMethod,
            Currency = command.Currency.Trim().ToUpperInvariant(),
            Subtotal = command.Amount,
            TotalAmount = command.Amount,
            Notes = $"System-generated for shipment {command.ShipmentId.Trim()}.",
            VerificationCode = DocumentSecurity.NewVerificationCode()
        };
        var lines = command.Lines.Select(x => new InvoiceLine
        {
            InvoiceId = invoice.Id,
            Description = x.Description.Trim(),
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            TaxRate = 0,
            LineTotal = Math.Round(x.Quantity * x.UnitPrice, 2)
        }).ToList();
        return new ShipmentInvoiceCreation(invoice, lines);
    }
}

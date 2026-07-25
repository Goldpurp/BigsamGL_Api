using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public sealed record CreateShipmentInvoiceCommand(
    Guid CustomerId,
    string CustomerName,
    string IssuerBranchCode,
    string Currency,
    decimal Amount,
    PaymentMethod PaymentMethod,
    IReadOnlyList<CreateShipmentInvoiceLineCommand> Lines,
    string ShipmentId);

public sealed record CreateShipmentInvoiceLineCommand(string Description, decimal Quantity,
    decimal UnitPrice);

public sealed record ShipmentInvoiceCreation(Invoice Invoice, IReadOnlyList<InvoiceLine> Lines);

/// <summary>
/// Creates the immutable commercial snapshot and system number for one customer shipment entry.
/// The caller persists the invoice, line and shipment entry in one unit of work.
/// </summary>
public interface IShipmentInvoiceService
{
    Task<ShipmentInvoiceCreation> CreateAsync(CreateShipmentInvoiceCommand command,
        CancellationToken cancellationToken);
}

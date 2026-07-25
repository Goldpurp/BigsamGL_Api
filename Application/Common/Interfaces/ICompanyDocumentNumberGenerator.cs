using Domain.Enums;

namespace Application.Common.Interfaces;

public interface ICompanyDocumentNumberGenerator
{
    Task<string> NextClientAsync(CancellationToken cancellationToken);
    Task<string> NextTransactionAsync(CancellationToken cancellationToken);
    Task<string> NextFinancialDocumentAsync(FinancialDocumentType type, CancellationToken cancellationToken);
    Task<string> NextCreditNoteAsync(CancellationToken cancellationToken);
    Task<string> NextPurchaseAsync(CancellationToken cancellationToken);
    Task<string> NextPayoutAsync(CancellationToken cancellationToken);
    Task<string> NextInventoryTransactionAsync(CancellationToken cancellationToken);
    Task<string> NextAppointmentAsync(CancellationToken cancellationToken);
    Task<string> NextEmailBroadcastAsync(CancellationToken cancellationToken);
    Task<string> NextOperationalManifestAsync(CancellationToken cancellationToken);
    Task<string> NextScheduledPickupAsync(CancellationToken cancellationToken);
    Task<string> NextCargoItemIssueAsync(CancellationToken cancellationToken);
}

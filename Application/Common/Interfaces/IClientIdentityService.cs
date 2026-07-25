using Domain.Enums;

namespace Application.Common.Interfaces;

public record ClientIdentityInput(string FullName, string? Email, string Phone, string? InvoiceNumber);
public record ClientMatchCandidate(Guid CustomerId, string FullName, string? Email, string Phone,
    IReadOnlyList<string> MatchingFields, decimal SimilarityScore, bool IsExact);
public record ClientDuplicateReviewResult(Guid ReviewId, bool RequiresConfirmation, bool CanCreateNew,
    bool HasExactConflict, Guid? SelectedCustomerId, DateTime ExpiresAt,
    string SuggestedAction, IReadOnlyList<ClientMatchCandidate> Matches);

public interface IClientIdentityService
{
    Task<ClientDuplicateReviewResult> CheckAsync(ClientIdentityInput input, Guid? excludeCustomerId,
        CancellationToken cancellationToken);
    Task<ClientDuplicateReviewResult?> ValidateNewClientAsync(ClientIdentityInput input, Guid? reviewId,
        Guid? excludeCustomerId, CancellationToken cancellationToken);
    Task<Guid?> GetSelectedExistingClientAsync(ClientIdentityInput input, Guid? reviewId,
        CancellationToken cancellationToken);
    Task<ClientDuplicateReviewResult> ResolveAsync(Guid reviewId, ClientDuplicateDecision decision,
        Guid? selectedCustomerId, string reason, CancellationToken cancellationToken);
}

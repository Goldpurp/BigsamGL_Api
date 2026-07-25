using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class ClientDuplicateReview : BaseEntity
{
    public string InputFingerprint { get; set; } = string.Empty;
    public string SubmittedFullName { get; set; } = string.Empty;
    public string SubmittedEmail { get; set; } = string.Empty;
    public string SubmittedPhone { get; set; } = string.Empty;
    public string SubmittedInvoiceNumber { get; set; } = string.Empty;
    public string MatchSummaryJson { get; set; } = "[]";
    public bool HasExactConflict { get; set; }
    public ClientDuplicateReviewStatus Status { get; set; } = ClientDuplicateReviewStatus.Pending;
    public Guid? SelectedCustomerId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

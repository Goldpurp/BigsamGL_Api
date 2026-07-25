using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class CargoItemIssue : BaseEntity
{
    public Guid BranchId { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public CargoItemIssueType Type { get; set; }
    public CargoItemIssueSeverity Severity { get; set; }
    public CargoItemIssueState State { get; set; } = CargoItemIssueState.Open;
    public Guid? CustomerShipmentId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? ManifestId { get; set; }
    public Guid? ManifestLineId { get; set; }
    public string ExpectedDetails { get; set; } = string.Empty;
    public string FoundDetails { get; set; } = string.Empty;
    public string? LastSeenPlace { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string InternalNote { get; set; } = string.Empty;
    public bool CustomerVisible { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ClientNotifiedAt { get; set; }
}

public sealed class CargoItemIssueEvidence : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid CargoItemIssueId { get; set; }
    public Guid? ShipmentDocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

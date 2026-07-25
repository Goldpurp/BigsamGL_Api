using Domain.Enums;

namespace Api.Controllers.Operations;

public sealed record CreateCargoItemIssueRequest(CargoItemIssueType Type,
    CargoItemIssueSeverity Severity, string? InvoiceNumber, Guid? ManifestId, Guid? ManifestLineId,
    string ExpectedDetails, string FoundDetails, string? LastSeenPlace, DateTime? LastSeenAt,
    string InternalNote, bool CustomerVisible);
public sealed record UpdateCargoItemIssueRequest(CargoItemIssueType Type,
    CargoItemIssueSeverity Severity, CargoItemIssueState State, string ExpectedDetails,
    string FoundDetails, string? LastSeenPlace, DateTime? LastSeenAt, string InternalNote,
    bool CustomerVisible, string Reason);
public sealed record AddCargoIssueEvidenceRequest(Guid? ShipmentDocumentId, string FileName,
    string ContentType, long SizeBytes, string Sha256);
public sealed record ResolveCargoItemIssueRequest(string Confirmation, string Resolution);
public sealed record NotifyCargoIssueClientRequest(string Confirmation, string Subject, string Body);
public sealed record CargoItemIssueResponse(Guid Id, string IssueNumber, long RowVersion,
    Guid BranchId, string Branch, CargoItemIssueType Type, CargoItemIssueSeverity Severity,
    CargoItemIssueState State, Guid? CustomerShipmentId, string? InvoiceNumber, Guid? ManifestId,
    Guid? ManifestLineId, string ExpectedDetails, string FoundDetails, string? LastSeenPlace,
    DateTime? LastSeenAt, string InternalNote, bool CustomerVisible, int EvidenceCount,
    DateTime? ResolvedAt, string? Resolution, DateTime? ClientNotifiedAt, DateTime CreatedAt,
    DateTime? LastModifiedAt);

using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Persistent report export state. The payload is retained briefly so downloads work across API instances.
/// </summary>
public sealed class ReportExportJob : BaseEntity
{
    public Guid RequestedByUserId { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    public string FiltersJson { get; set; } = "{}";
    public ReportExportStatus Status { get; set; } = ReportExportStatus.Processing;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public byte[]? FileContent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class LegacyImportBatch : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public LegacyImportStatus Status { get; set; } = LegacyImportStatus.Previewed;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int DuplicateRows { get; set; }
    public int ErrorRows { get; set; }
    public int ImportedRows { get; set; }
    public DateTime? CommittedAt { get; set; }
    public string? CommittedByUserId { get; set; }
    public string? FailureReason { get; set; }
    public string MappingJson { get; set; } = "{}";
}

public sealed class LegacyImportRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LegacyImportBatchId { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public LegacyImportEntityType EntityType { get; set; }
    public LegacyImportRowStatus Status { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public string? Message { get; set; }
    public Guid? ImportedEntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

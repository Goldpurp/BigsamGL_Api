using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces;

public sealed record LegacyImportPreviewResult(LegacyImportBatch Batch,
    IReadOnlyDictionary<LegacyImportEntityType, int> RowsByType,
    IReadOnlyList<LegacyImportRow> SampleRows);

public sealed record LegacyImportCommitResult(Guid BatchId, LegacyImportStatus Status,
    int ImportedRows, int SkippedRows, int ErrorRows);

public interface ILegacyDataImportService
{
    Task<LegacyImportPreviewResult> PreviewAsync(Stream stream, string fileName, Guid actorUserId,
        CancellationToken cancellationToken);
    Task<LegacyImportCommitResult> CommitAsync(Guid batchId, Guid actorUserId,
        CancellationToken cancellationToken);
}

namespace Api.Controllers;

public sealed record ReportRequest(string ReportKey, DateTime? From = null, DateTime? To = null,
    IReadOnlyList<string>? BranchCodes = null, string? Currency = null, int PreviewRows = 100);

public sealed record ReportPreviewSheet(string Name, IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows, int ReturnedRows);

public sealed record ReportPreviewResponse(string ReportKey, DateTime GeneratedAt,
    IReadOnlyList<ReportPreviewSheet> Sheets);

public sealed record ReportExportJobResponse(Guid JobId, string ReportKey, string Status,
    string FileName, DateTime RequestedAt, DateTime? CompletedAt, DateTime ExpiresAt,
    string? ErrorMessage, string? DownloadUrl);

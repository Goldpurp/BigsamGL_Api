using Domain.Enums;

namespace Application.Common.Interfaces;

public sealed record OfficeCell(object? Value, OfficeCellKind Kind = OfficeCellKind.Text);

public sealed record OfficeSheet(string Name, IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<OfficeCell>> Rows, IReadOnlyList<OfficeCell>? Totals = null,
    string? Subtitle = null);

public sealed record OfficeWorkbook(string Title, DateTime GeneratedAt,
    IReadOnlyList<OfficeSheet> Sheets);

public sealed record BusinessDocument(string Title, string Number, string CompanyName,
    IReadOnlyList<KeyValuePair<string, string>> Details, OfficeSheet? Table,
    IReadOnlyList<KeyValuePair<string, string>> Totals, string? VerificationCode = null,
    string? Footer = null);

public sealed record RenderedDocument(byte[] Content, string ContentType, string FileExtension);

public interface IOfficeDocumentService
{
    RenderedDocument Render(BusinessDocument document, BusinessDocumentFormat format);
    byte[] RenderWorkbook(OfficeWorkbook workbook);
}

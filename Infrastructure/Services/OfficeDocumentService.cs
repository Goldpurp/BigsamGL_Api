using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Application.Common.Interfaces;
using Domain.Enums;

namespace Infrastructure.Services;

public sealed class OfficeDocumentService : IOfficeDocumentService
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public RenderedDocument Render(BusinessDocument document, BusinessDocumentFormat format) =>
        format switch
        {
            BusinessDocumentFormat.Pdf => new(RenderPdf(document), "application/pdf", "pdf"),
            BusinessDocumentFormat.Xlsx => new(RenderWorkbook(new OfficeWorkbook(
                document.Title, DateTime.UtcNow,
                DocumentSheets(document))), XlsxContentType, "xlsx"),
            BusinessDocumentFormat.Docx => new(RenderDocx(document), DocxContentType, "docx"),
            BusinessDocumentFormat.Csv => new(RenderCsv(document), "text/csv; charset=utf-8", "csv"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    public byte[] RenderWorkbook(OfficeWorkbook workbook)
    {
        if (workbook.Sheets.Count == 0) throw new ArgumentException("A workbook needs at least one sheet.");
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes(workbook.Sheets.Count));
            Write(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
                  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
                </Relationships>
                """);
            Write(archive, "docProps/core.xml", CoreProperties(workbook.Title, workbook.GeneratedAt));
            Write(archive, "docProps/app.xml", AppProperties(workbook.Sheets.Select(x => x.Name)));
            Write(archive, "xl/workbook.xml", WorkbookXml(workbook.Sheets));
            Write(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(workbook.Sheets.Count));
            Write(archive, "xl/styles.xml", StylesXml());
            for (var i = 0; i < workbook.Sheets.Count; i++)
                Write(archive, $"xl/worksheets/sheet{i + 1}.xml",
                    WorksheetXml(workbook.Title, workbook.GeneratedAt, workbook.Sheets[i]));
        }
        return output.ToArray();
    }

    private static IReadOnlyList<OfficeSheet> DocumentSheets(BusinessDocument document)
    {
        var rows = new List<IReadOnlyList<OfficeCell>>();
        foreach (var detail in document.Details)
            rows.Add([new(detail.Key), new(detail.Value)]);
        foreach (var total in document.Totals)
            rows.Add([new(total.Key), new(total.Value)]);

        var sheets = new List<OfficeSheet>
        {
            new(CleanSheetName(document.Title), ["Field", "Value"], rows, Subtitle: document.Number)
        };
        if (document.Table is not null)
        {
            sheets.Add(document.Table with
            {
                Name = CleanSheetName($"{document.Title} items"),
                Subtitle = document.Number
            });
        }
        return sheets;
    }

    private static string WorksheetXml(string title, DateTime generatedAt, OfficeSheet sheet)
    {
        var columns = Math.Max(1, sheet.Columns.Count);
        var rows = new List<string>();
        var lastColumn = ColumnName(columns);
        rows.Add(Row(1, [TextCell("A1", title, 1)]));
        rows.Add(Row(2, [TextCell("A2",
            $"{sheet.Subtitle ?? sheet.Name} · Generated {generatedAt:yyyy-MM-dd HH:mm} UTC", 5)]));
        rows.Add(Row(4, sheet.Columns.Select((value, index) =>
            TextCell($"{ColumnName(index + 1)}4", value, 2))));
        var rowNumber = 5;
        foreach (var sourceRow in sheet.Rows)
        {
            var cells = sourceRow.Take(columns).Select((value, index) =>
                Cell($"{ColumnName(index + 1)}{rowNumber}", value)).ToArray();
            rows.Add(Row(rowNumber++, cells));
        }
        if (sheet.Totals is { Count: > 0 })
        {
            var totalCells = sheet.Totals.Take(columns).Select((value, index) =>
                Cell($"{ColumnName(index + 1)}{rowNumber}", value, true)).ToArray();
            rows.Add(Row(rowNumber, totalCells));
        }
        var widths = Enumerable.Range(1, columns).Select(index =>
            $"<col min=\"{index}\" max=\"{index}\" width=\"{(index == 1 ? 24 : 18)}\" customWidth=\"1\"/>");
        var dimension = $"A1:{lastColumn}{Math.Max(4, rowNumber)}";
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <dimension ref="{dimension}"/>
              <sheetViews><sheetView workbookViewId="0" showGridLines="0">
                <pane ySplit="4" topLeftCell="A5" activePane="bottomLeft" state="frozen"/>
              </sheetView></sheetViews>
              <cols>{string.Concat(widths)}</cols>
              <sheetData>{string.Concat(rows)}</sheetData>
              <autoFilter ref="A4:{lastColumn}{Math.Max(4, rowNumber - 1)}"/>
              <mergeCells count="2"><mergeCell ref="A1:{lastColumn}1"/><mergeCell ref="A2:{lastColumn}2"/></mergeCells>
              <pageMargins left="0.3" right="0.3" top="0.5" bottom="0.5" header="0.2" footer="0.2"/>
              <pageSetup orientation="landscape" fitToWidth="1" fitToHeight="0"/>
            </worksheet>
            """;
    }

    private static string Cell(string reference, OfficeCell value, bool total = false)
    {
        if (value.Value is null) return $"<c r=\"{reference}\"/>";
        var style = total ? 6 : value.Kind switch
        {
            OfficeCellKind.Money => 3,
            OfficeCellKind.Date => 4,
            OfficeCellKind.DateTime => 7,
            OfficeCellKind.Integer => 8,
            OfficeCellKind.Number => 9,
            _ => 0
        };
        if (value.Kind is OfficeCellKind.Number or OfficeCellKind.Money or OfficeCellKind.Integer)
            return $"<c r=\"{reference}\" s=\"{style}\"><v>{Convert.ToDecimal(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)}</v></c>";
        if (value.Kind is OfficeCellKind.Date or OfficeCellKind.DateTime)
        {
            var date = Convert.ToDateTime(value.Value, CultureInfo.InvariantCulture);
            return $"<c r=\"{reference}\" s=\"{style}\"><v>{date.ToOADate().ToString(CultureInfo.InvariantCulture)}</v></c>";
        }
        return TextCell(reference, Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? "", style);
    }

    private static string TextCell(string reference, string value, int style = 0) =>
        $"<c r=\"{reference}\" t=\"inlineStr\" s=\"{style}\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>";

    private static string Row(int index, IEnumerable<string> cells) =>
        $"<row r=\"{index}\">{string.Concat(cells)}</row>";

    private static string WorkbookXml(IReadOnlyList<OfficeSheet> sheets)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = sheets.Select((sheet, index) => UniqueSheetName(sheet.Name, index, used)).ToArray();
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <bookViews><workbookView xWindow="0" yWindow="0" windowWidth="24000" windowHeight="14000"/></bookViews>
              <sheets>{string.Concat(names.Select((name, index) => $"<sheet name=\"{Escape(name)}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>"))}</sheets>
              <calcPr calcId="191029" fullCalcOnLoad="1"/>
            </workbook>
            """;
    }

    private static string WorkbookRelationships(int count) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          {string.Concat(Enumerable.Range(1, count).Select(i => $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>"))}
          <Relationship Id="rId{count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string ContentTypes(int count) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
          {string.Concat(Enumerable.Range(1, count).Select(i => $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"))}
          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
          <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
        </Types>
        """;

    private static string StylesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="3"><numFmt numFmtId="164" formatCode="#,##0.00"/><numFmt numFmtId="165" formatCode="yyyy-mm-dd"/><numFmt numFmtId="166" formatCode="yyyy-mm-dd hh:mm"/></numFmts>
          <fonts count="4">
            <font><sz val="10"/><name val="Aptos"/></font>
            <font><b/><sz val="18"/><color rgb="FF123A63"/><name val="Aptos Display"/></font>
            <font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Aptos"/></font>
            <font><b/><sz val="10"/><name val="Aptos"/></font>
          </fonts>
          <fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF123A63"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEAF1F7"/><bgColor indexed="64"/></patternFill></fill></fills>
          <borders count="2"><border/><border><bottom style="thin"><color rgb="FFD4DEE8"/></bottom></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="10">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"><alignment vertical="center"/></xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"><alignment horizontal="right"/></xf>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"><alignment vertical="center"/></xf>
            <xf numFmtId="164" fontId="3" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1"/>
            <xf numFmtId="166" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
            <xf numFmtId="1" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
            <xf numFmtId="2" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;

    private static byte[] RenderDocx(BusinessDocument document)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Write(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);
            Write(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);
            var paragraphs = new List<string>
            {
                WordParagraph(document.CompanyName, true, 32),
                WordParagraph(document.Title, true, 28),
                WordParagraph(document.Number, true, 24)
            };
            paragraphs.AddRange(document.Details.Select(x => WordParagraph($"{x.Key}: {x.Value}", false, 20)));
            if (document.Table is not null)
                paragraphs.Add(WordTable(document.Table));
            paragraphs.AddRange(document.Totals.Select(x => WordParagraph($"{x.Key}: {x.Value}", true, 20)));
            if (!string.IsNullOrWhiteSpace(document.VerificationCode))
                paragraphs.Add(WordParagraph($"Verification code: {document.VerificationCode}", false, 18));
            if (!string.IsNullOrWhiteSpace(document.Footer))
                paragraphs.Add(WordParagraph(document.Footer, false, 18));
            Write(archive, "word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>{string.Concat(paragraphs)}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="850" w:right="850" w:bottom="850" w:left="850"/></w:sectPr></w:body>
                </w:document>
                """);
        }
        return output.ToArray();
    }

    private static string WordTable(OfficeSheet sheet)
    {
        static string row(IEnumerable<string> values) =>
            $"<w:tr>{string.Concat(values.Select(value => $"<w:tc><w:tcPr><w:tcW w:w=\"2200\" w:type=\"dxa\"/></w:tcPr>{WordParagraph(value, false, 18)}</w:tc>"))}</w:tr>";
        var rows = new List<string> { row(sheet.Columns) };
        rows.AddRange(sheet.Rows.Select(x => row(x.Select(cell =>
            Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty))));
        return $"<w:tbl><w:tblPr><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\"/><w:left w:val=\"single\" w:sz=\"4\"/><w:bottom w:val=\"single\" w:sz=\"4\"/><w:right w:val=\"single\" w:sz=\"4\"/><w:insideH w:val=\"single\" w:sz=\"2\"/><w:insideV w:val=\"single\" w:sz=\"2\"/></w:tblBorders></w:tblPr>{string.Concat(rows)}</w:tbl>";
    }

    private static string WordParagraph(string value, bool bold, int size) =>
        $"<w:p><w:r><w:rPr>{(bold ? "<w:b/>" : "")}<w:sz w:val=\"{size}\"/></w:rPr><w:t xml:space=\"preserve\">{Escape(value)}</w:t></w:r></w:p>";

    private static byte[] RenderPdf(BusinessDocument document)
    {
        var lines = new List<string>
        {
            document.CompanyName, document.Title, document.Number,
            $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", string.Empty
        };
        lines.AddRange(document.Details.Select(x => $"{x.Key}: {x.Value}"));
        if (document.Table is not null)
        {
            lines.Add(string.Empty);
            lines.Add(string.Join(" | ", document.Table.Columns));
            lines.Add(new string('-', 90));
            lines.AddRange(document.Table.Rows.Select(row => string.Join(" | ", row.Select(cell =>
                Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty))));
        }
        lines.Add(string.Empty);
        lines.AddRange(document.Totals.Select(x => $"{x.Key}: {x.Value}"));
        if (!string.IsNullOrWhiteSpace(document.VerificationCode))
            lines.Add($"Verification code: {document.VerificationCode}");
        if (!string.IsNullOrWhiteSpace(document.Footer)) lines.Add(document.Footer);
        var printableLines = lines.SelectMany(WrapPdfLine).ToArray();
        var pages = printableLines.Chunk(48).Select(x => x.ToArray()).ToArray();
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(0, pages.Length).Select(i => $"{4 + i * 2} 0 R"))}] /Count {pages.Length} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        for (var index = 0; index < pages.Length; index++)
        {
            var content =
                "q 0.94 g BT /F1 56 Tf 145 430 Td (BiGSAM GL) Tj ET Q " +
                "BT /F1 10 Tf 46 800 Td 14 TL " +
                string.Join(" Tj T*", pages[index].Select(line => $"({PdfEscape(line)})")) +
                $" Tj ET BT /F1 8 Tf 46 24 Td (BiGSAM Global Logistics | Page {index + 1} of {pages.Length}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + index * 2} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { NewLine = "\n" };
        writer.Write("%PDF-1.4\n");
        writer.Flush();
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            writer.Flush();
        }
        var xref = stream.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) writer.Write($"{offset:D10} 00000 n \n");
        writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] RenderCsv(BusinessDocument document)
    {
        static string field(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        var rows = new List<string>
        {
            string.Join(',', field("Document"), field(document.Title)),
            string.Join(',', field("Document number"), field(document.Number)),
            string.Join(',', field("Company"), field(document.CompanyName)),
            string.Join(',', field("Generated"), field(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)))
        };
        rows.AddRange(document.Details.Select(x => string.Join(',', field(x.Key), field(x.Value))));
        if (document.Table is not null)
        {
            rows.Add(string.Empty);
            rows.Add(string.Join(',', document.Table.Columns.Select(field)));
            rows.AddRange(document.Table.Rows.Select(row =>
                string.Join(',', row.Select(cell => field(cell.Value)))));
        }
        rows.AddRange(document.Totals.Select(x => string.Join(',', field(x.Key), field(x.Value))));
        var encoding = new UTF8Encoding(true);
        return encoding.GetPreamble().Concat(
            encoding.GetBytes(string.Join("\r\n", rows))).ToArray();
    }

    private static string CoreProperties(string title, DateTime created) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <dc:title>{Escape(title)}</dc:title><dc:creator>BiGSAM Global Logistics</dc:creator>
          <dcterms:created xsi:type="dcterms:W3CDTF">{created.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>
        </cp:coreProperties>
        """;

    private static string AppProperties(IEnumerable<string> sheets) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
          <Application>BiGSAM Global Logistics</Application><AppVersion>1.0</AppVersion>
          <TitlesOfParts><vt:vector size="{sheets.Count()}" baseType="lpstr">{string.Concat(sheets.Select(x => $"<vt:lpstr>{Escape(x)}</vt:lpstr>"))}</vt:vector></TitlesOfParts>
        </Properties>
        """;

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }

    private static string Escape(string? value) => SecurityElementEscape(value ?? string.Empty);
    private static string SecurityElementEscape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(character switch
            {
                '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&quot;",
                '\'' => "&apos;", _ when XmlConvert.IsXmlChar(character) => character, _ => ' '
            });
        return builder.ToString();
    }

    private static string PdfEscape(string value)
    {
        var ascii = new string(value.Select(x => x is >= ' ' and <= '~' ? x : ' ').ToArray());
        return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static IEnumerable<string> WrapPdfLine(string value)
    {
        const int maximumCharacters = 82;
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return string.Empty;
            yield break;
        }

        var remaining = value.Trim();
        while (remaining.Length > maximumCharacters)
        {
            var split = remaining.LastIndexOf(' ', maximumCharacters);
            if (split < maximumCharacters / 2) split = maximumCharacters;
            yield return remaining[..split].TrimEnd();
            remaining = remaining[split..].TrimStart();
        }
        yield return remaining;
    }

    private static string ColumnName(int number)
    {
        var result = "";
        while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; }
        return result;
    }

    private static string CleanSheetName(string value)
    {
        var clean = new string(value.Where(x => !"[]:*?/\\".Contains(x)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "Report" : clean[..Math.Min(31, clean.Length)];
    }

    private static string UniqueSheetName(string value, int index, ISet<string> used)
    {
        var root = CleanSheetName(value);
        var candidate = root;
        var suffix = 2;
        while (!used.Add(candidate))
        {
            var extra = $" {suffix++}";
            candidate = root[..Math.Min(root.Length, 31 - extra.Length)] + extra;
        }
        return candidate;
    }
}

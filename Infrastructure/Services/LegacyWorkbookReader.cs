using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Infrastructure.Services;

internal sealed record LegacyWorkbookSheet(string Name,
    IReadOnlyList<(int RowNumber, IReadOnlyDictionary<string, string> Values)> Rows);

internal static class LegacyWorkbookReader
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace DocumentRelationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static IReadOnlyList<LegacyWorkbookSheet> Read(byte[] file, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".xlsx" => ReadXlsx(file),
            ".csv" => new[] { ReadCsv(file, Path.GetFileNameWithoutExtension(fileName)) },
            _ => throw new ArgumentException("Legacy import accepts .xlsx or .csv files. Convert old .xls files to .xlsx first.")
        };
    }

    private static IReadOnlyList<LegacyWorkbookSheet> ReadXlsx(byte[] file)
    {
        using var memory = new MemoryStream(file, writable: false);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var relationshipDocument = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var targets = relationshipDocument.Root?.Elements(Relationships + "Relationship")
            .Where(x => x.Attribute("Id") is not null && x.Attribute("Target") is not null)
            .ToDictionary(x => x.Attribute("Id")!.Value, x => NormalizeWorksheetPath(x.Attribute("Target")!.Value))
            ?? new Dictionary<string, string>();
        var sharedStrings = ReadSharedStrings(archive);
        var sheets = new List<LegacyWorkbookSheet>();
        foreach (var sheet in workbook.Descendants(Spreadsheet + "sheet"))
        {
            var name = sheet.Attribute("name")?.Value ?? "Sheet";
            var relationshipId = sheet.Attribute(DocumentRelationships + "id")?.Value;
            if (relationshipId is null || !targets.TryGetValue(relationshipId, out var path)) continue;
            var worksheet = LoadXml(archive, path);
            sheets.Add(ParseWorksheet(name, worksheet, sharedStrings));
        }
        if (sheets.Count == 0) throw new ArgumentException("The Excel workbook contains no readable worksheets.");
        return sheets;
    }

    private static LegacyWorkbookSheet ParseWorksheet(string name, XDocument worksheet,
        IReadOnlyList<string> sharedStrings)
    {
        var rawRows = new List<(int RowNumber, Dictionary<int, string> Cells)>();
        foreach (var row in worksheet.Descendants(Spreadsheet + "row"))
        {
            var rowNumber = int.TryParse(row.Attribute("r")?.Value, out var parsed) ? parsed : rawRows.Count + 1;
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                if (string.IsNullOrWhiteSpace(reference)) continue;
                var column = ColumnIndex(reference);
                var type = cell.Attribute("t")?.Value;
                string value;
                if (type == "inlineStr")
                    value = string.Concat(cell.Descendants(Spreadsheet + "t").Select(x => x.Value));
                else
                {
                    var raw = cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
                    value = type == "s" && int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count
                        ? sharedStrings[index] : raw;
                }
                if (!string.IsNullOrWhiteSpace(value)) cells[column] = value.Trim();
            }
            if (cells.Count > 0) rawRows.Add((rowNumber, cells));
        }
        if (rawRows.Count == 0) return new LegacyWorkbookSheet(name,
            Array.Empty<(int, IReadOnlyDictionary<string, string>)>());
        var headerRow = rawRows[0];
        var headers = headerRow.Cells.ToDictionary(x => x.Key, x => NormalizeHeader(x.Value));
        var rows = new List<(int, IReadOnlyDictionary<string, string>)>();
        foreach (var row in rawRows.Skip(1))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row.Cells)
            {
                if (!headers.TryGetValue(cell.Key, out var header) || string.IsNullOrWhiteSpace(header) ||
                    values.ContainsKey(header)) continue;
                values[header] = cell.Value;
            }
            if (values.Values.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add((row.RowNumber, values));
        }
        return new LegacyWorkbookSheet(name, rows);
    }

    private static LegacyWorkbookSheet ReadCsv(byte[] file, string name)
    {
        var text = Encoding.UTF8.GetString(file);
        var records = ParseCsv(text);
        if (records.Count == 0) return new LegacyWorkbookSheet(name,
            Array.Empty<(int, IReadOnlyDictionary<string, string>)>());
        var headers = records[0].Select(NormalizeHeader).ToList();
        var rows = new List<(int, IReadOnlyDictionary<string, string>)>();
        for (var rowIndex = 1; rowIndex < records.Count; rowIndex++)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < Math.Min(headers.Count, records[rowIndex].Count); column++)
                if (!string.IsNullOrWhiteSpace(headers[column]) && !values.ContainsKey(headers[column]))
                    values[headers[column]] = records[rowIndex][column].Trim();
            if (values.Values.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add((rowIndex + 1, values));
        }
        return new LegacyWorkbookSheet(name, rows);
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                { field.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            { row.Add(field.ToString()); field.Clear(); }
            else if ((character == '\n' || character == '\r') && !quoted)
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                row.Add(field.ToString()); field.Clear();
                if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row);
                row = new List<string>();
            }
            else field.Append(character);
        }
        row.Add(field.ToString());
        if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row);
        return rows;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return Array.Empty<string>();
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(Spreadsheet + "si")
            .Select(x => string.Concat(x.Descendants(Spreadsheet + "t").Select(y => y.Value))).ToList();
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new ArgumentException($"Excel workbook is missing {path}.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string NormalizeWorksheetPath(string target)
    {
        var value = target.Replace('\\', '/').TrimStart('/');
        while (value.StartsWith("../", StringComparison.Ordinal)) value = value[3..];
        return value.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? value : $"xl/{value}";
    }

    private static int ColumnIndex(string reference)
    {
        var result = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        return result - 1;
    }

    internal static string NormalizeHeader(string value) =>
        new(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    internal static string Value(IReadOnlyDictionary<string, string> row, params string[] names)
    {
        foreach (var name in names)
            if (row.TryGetValue(NormalizeHeader(name), out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        return string.Empty;
    }

    internal static DateTime? Date(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ||
            DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed))
            return parsed.ToUniversalTime();
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) &&
               serial is > 1 and < 2958466
            ? System.DateTime.FromOADate(serial).ToUniversalTime() : null;
    }

    internal static decimal? Decimal(string value) => decimal.TryParse(value, NumberStyles.Any,
        CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}

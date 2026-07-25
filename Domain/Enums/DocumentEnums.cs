namespace Domain.Enums;

public enum BusinessDocumentFormat
{
    Pdf = 0,
    Xlsx = 1,
    Docx = 2,
    Csv = 3
}

public enum ReportExportStatus
{
    Processing = 0,
    Completed = 1,
    Failed = 2
}

public enum OfficeCellKind
{
    Text = 0,
    Number = 1,
    Money = 2,
    Date = 3,
    DateTime = 4,
    Integer = 5
}

using Microsoft.AspNetCore.Http;

namespace Api.Controllers;

/// <summary>
/// Multipart request used to stage an historical Excel or CSV import.
/// Keeping the file inside a form model gives OpenAPI a valid request-body schema.
/// </summary>
public sealed class LegacyImportUploadRequest
{
    public required IFormFile File { get; init; }
}

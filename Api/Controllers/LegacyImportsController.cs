using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/legacy-imports")]
[Route("api/imports")]
public sealed class LegacyImportsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILegacyDataImportService _imports;
    private readonly IWebHostEnvironment _environment;
    private readonly IOfficeDocumentService _documents;
    public LegacyImportsController(IApplicationDbContext context, ILegacyDataImportService imports,
        IWebHostEnvironment environment, IOfficeDocumentService documents)
    {
        _context = context; _imports = imports; _environment = environment; _documents = documents;
    }

    [HttpGet("template")]
    public IActionResult Template()
    {
        var path = Path.Combine(_environment.ContentRootPath, "Templates", "BigsamGL_Legacy_Import_Template.xlsx");
        if (!System.IO.File.Exists(path)) return NotFound("Legacy import template is not installed.");
        return PhysicalFile(path, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "BigsamGL_Legacy_Import_Template.xlsx");
    }

    [HttpPost("preview")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public async Task<ActionResult<LegacyImportPreviewResult>> Preview(
        [FromForm] LegacyImportUploadRequest request,
        CancellationToken cancellationToken)
    {
        var file = request.File;
        if (file.Length == 0) return BadRequest("Select a non-empty .xlsx or .csv file.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
            return BadRequest("Legacy import accepts .xlsx or .csv. Convert old .xls workbooks to .xlsx first.");
        await using var stream = file.OpenReadStream();
        return Ok(await _imports.PreviewAsync(stream, file.FileName, CurrentUserId, cancellationToken));
    }

    [HttpPost("{batchId:guid}/commit")]
    public async Task<ActionResult<LegacyImportCommitResult>> Commit(Guid batchId,
        CancellationToken cancellationToken) => Ok(await _imports.CommitAsync(batchId, CurrentUserId,
            cancellationToken));

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] LegacyImportStatus? status,
        [FromQuery] string? fileName, [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.LegacyImportBatches.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var value = fileName.Trim().ToLowerInvariant();
            query = query.Where(x => x.FileName.ToLower().Contains(value));
        }
        if (createdFrom.HasValue) query = query.Where(x => x.CreatedAt >= createdFrom);
        if (createdTo.HasValue) query = query.Where(x => x.CreatedAt < createdTo.Value.AddDays(1));
        var projected = query.Select(x => new
        {
            x.Id,
            x.FileName,
            x.Status,
            x.TotalRows,
            x.ValidRows,
            x.DuplicateRows,
            x.ErrorRows,
            x.ImportedRows,
            x.CreatedAt,
            x.CommittedAt,
            ImportedBy = x.CommittedByUserId ?? x.CreatedBy ?? "System",
            CreatedClients = _context.LegacyImportRows.Count(row =>
                row.LegacyImportBatchId == x.Id &&
                row.EntityType == LegacyImportEntityType.Client &&
                row.Status == LegacyImportRowStatus.Imported),
            CreatedInvoices = _context.LegacyImportRows.Count(row =>
                row.LegacyImportBatchId == x.Id &&
                row.EntityType == LegacyImportEntityType.Invoice &&
                row.Status == LegacyImportRowStatus.Imported),
            CreatedCargo = _context.LegacyImportRows.Count(row =>
                row.LegacyImportBatchId == x.Id &&
                row.EntityType == LegacyImportEntityType.Cargo &&
                row.Status == LegacyImportRowStatus.Imported),
            Skipped = x.DuplicateRows + x.ErrorRows
        });
        return Ok(await projected.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("{batchId:guid}")]
    [HttpGet("{batchId:guid}/rows")]
    public async Task<ActionResult> Get(Guid batchId, [FromQuery] LegacyImportEntityType? entityType,
        [FromQuery] LegacyImportRowStatus? rowStatus, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100,
        [FromQuery] bool includeTotal = true, CancellationToken cancellationToken = default)
    {
        var batch = await _context.LegacyImportBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId,
            cancellationToken) ?? throw new KeyNotFoundException("Legacy import batch not found.");
        var query = _context.LegacyImportRows.AsNoTracking().Where(x => x.LegacyImportBatchId == batchId);
        if (entityType.HasValue) query = query.Where(x => x.EntityType == entityType);
        if (rowStatus.HasValue) query = query.Where(x => x.Status == rowStatus);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.SourceKey.ToLower().Contains(value) ||
                (x.Message != null && x.Message.ToLower().Contains(value)));
        }
        var rows = await query.OrderBy(x => x.EntityType).ThenBy(x => x.RowNumber)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
        return Ok(new { batch, rows });
    }

    [HttpGet("{batchId:guid}/sheets")]
    public async Task<ActionResult> Sheets(Guid batchId, CancellationToken cancellationToken)
    {
        if (!await _context.LegacyImportBatches.AsNoTracking().AnyAsync(x => x.Id == batchId,
            cancellationToken)) return NotFound();
        var sheets = await _context.LegacyImportRows.AsNoTracking()
            .Where(x => x.LegacyImportBatchId == batchId)
            .GroupBy(x => new { x.SheetName, x.EntityType })
            .Select(x => new { x.Key.SheetName, x.Key.EntityType, Rows = x.Count() })
            .OrderBy(x => x.SheetName).ToListAsync(cancellationToken);
        return Ok(sheets);
    }

    [HttpPut("{batchId:guid}/mapping")]
    public async Task<ActionResult> Mapping(Guid batchId,
        [FromBody] Dictionary<string, Dictionary<string, string>> mapping,
        CancellationToken cancellationToken)
    {
        var batch = await _context.LegacyImportBatches.FirstOrDefaultAsync(x => x.Id == batchId,
            cancellationToken);
        if (batch is null) return NotFound();
        if (batch.Status != LegacyImportStatus.Previewed)
            return Conflict("Mapping cannot change after an import is committed.");
        batch.MappingJson = System.Text.Json.JsonSerializer.Serialize(mapping);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { batch.Id, Mapping = mapping,
            Note = "The uploaded workbook was already normalized using the saved aliases. Re-upload to apply different source columns." });
    }

    [HttpPost("{batchId:guid}/validate")]
    public async Task<ActionResult> Validate(Guid batchId, CancellationToken cancellationToken) =>
        await Reconciliation(batchId, cancellationToken);

    [HttpGet("{batchId:guid}/status")]
    public async Task<ActionResult> Status(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _context.LegacyImportBatches.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == batchId, cancellationToken);
        return batch is null ? NotFound() : Ok(new
        {
            batch.Id, batch.Status, batch.TotalRows, batch.ValidRows, batch.DuplicateRows,
            batch.ErrorRows, batch.ImportedRows, batch.CommittedAt, batch.FailureReason
        });
    }

    [HttpGet("{batchId:guid}/reconciliation")]
    public async Task<ActionResult> Reconciliation(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _context.LegacyImportBatches.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == batchId, cancellationToken);
        if (batch is null) return NotFound();
        var rows = await _context.LegacyImportRows.AsNoTracking()
            .Where(x => x.LegacyImportBatchId == batchId)
            .GroupBy(x => new { x.EntityType, x.Status })
            .Select(x => new { x.Key.EntityType, x.Key.Status, Count = x.Count() })
            .ToListAsync(cancellationToken);
        return Ok(new
        {
            batch.Id, batch.FileName, batch.Status, batch.TotalRows,
            ByEntity = rows.GroupBy(x => x.EntityType).Select(x => new
            {
                EntityType = x.Key, Total = x.Sum(y => y.Count),
                Statuses = x.Select(y => new { y.Status, y.Count })
            })
        });
    }

    [HttpGet("{batchId:guid}/errors.xlsx")]
    public async Task<IActionResult> Errors(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _context.LegacyImportBatches.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == batchId, cancellationToken);
        if (batch is null) return NotFound();
        var rows = await _context.LegacyImportRows.AsNoTracking().Where(x =>
                x.LegacyImportBatchId == batchId &&
                (x.Status == LegacyImportRowStatus.Error ||
                 x.Status == LegacyImportRowStatus.Duplicate ||
                 x.Status == LegacyImportRowStatus.Skipped))
            .OrderBy(x => x.SheetName).ThenBy(x => x.RowNumber).Take(50_000)
            .ToListAsync(cancellationToken);
        var sheet = new OfficeSheet("Import issues",
            ["Sheet", "Row", "Entity", "Status", "Source key", "Message"],
            rows.Select(x => (IReadOnlyList<OfficeCell>)
            [
                new(x.SheetName), new(x.RowNumber, OfficeCellKind.Integer),
                new(x.EntityType.ToString()), new(x.Status.ToString()),
                new(x.SourceKey), new(x.Message ?? "")
            ]).ToList(), Subtitle: batch.FileName);
        var content = _documents.RenderWorkbook(new OfficeWorkbook(
            "BiGSAM legacy import reconciliation", DateTime.UtcNow, [sheet]));
        return File(content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"import-{batch.Id:N}-errors.xlsx");
    }
}

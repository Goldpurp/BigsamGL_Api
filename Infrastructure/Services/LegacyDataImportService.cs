using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class LegacyDataImportService : ILegacyDataImportService
{
    private const int MaximumFileBytes = 20 * 1024 * 1024;
    private const int MaximumRows = 20_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _context;
    public LegacyDataImportService(ApplicationDbContext context) => _context = context;

    public async Task<LegacyImportPreviewResult> PreviewAsync(Stream stream, string fileName, Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Import file name is required.");
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0) throw new ArgumentException("Import file is empty.");
        if (memory.Length > MaximumFileBytes) throw new ArgumentException("Import file cannot exceed 20 MB.");
        var bytes = memory.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var previous = await _context.LegacyImportBatches.AsNoTracking().FirstOrDefaultAsync(x =>
            x.FileSha256 == hash, cancellationToken);
        if (previous is not null) return await ResultAsync(previous, cancellationToken);

        var sheets = LegacyWorkbookReader.Read(bytes, fileName).Where(x =>
        {
            var name = LegacyWorkbookReader.NormalizeHeader(x.Name);
            return name is not ("instructions" or "referencevalues" or "lists" or "readme") && x.Rows.Count > 0;
        }).ToList();
        var totalRows = sheets.Sum(x => x.Rows.Count);
        if (totalRows == 0) throw new ArgumentException("No import data rows were found.");
        if (totalRows > MaximumRows)
            throw new ArgumentException($"A single import is limited to {MaximumRows:N0} rows. Split the workbook and preview each part.");

        var customers = await _context.Customers.AsNoTracking().Select(x => new ExistingCustomer(x.Id,
            x.NormalizedFullName, x.NormalizedEmail ?? string.Empty, x.NormalizedPhone))
            .ToListAsync(cancellationToken);
        var invoiceNumbers = await _context.Invoices.AsNoTracking().Select(x => new { x.Id, x.InvoiceNumber })
            .ToDictionaryAsync(x => Normalize(x.InvoiceNumber), x => x.Id, cancellationToken);
        var trackingNumbers = await _context.CargoItems.AsNoTracking().Select(x => new { x.Id, x.TrackingNumber })
            .ToDictionaryAsync(x => Normalize(x.TrackingNumber), x => x.Id, cancellationToken);
        var branches = await _context.Branches.AsNoTracking().Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var incomingClients = new List<LegacyClientPayload>();
        var incomingInvoiceNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var incomingTrackingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<LegacyImportRow>(totalRows);

        foreach (var sheet in sheets)
        {
            var type = DetectType(sheet);
            foreach (var source in sheet.Rows)
            {
                var row = type switch
                {
                    LegacyImportEntityType.Client => PreviewClient(sheet.Name, source.RowNumber, source.Values,
                        customers, incomingClients),
                    LegacyImportEntityType.Invoice => PreviewInvoice(sheet.Name, source.RowNumber, source.Values,
                        customers, incomingClients, invoiceNumbers, incomingInvoiceNumbers, branches),
                    LegacyImportEntityType.Cargo => PreviewCargo(sheet.Name, source.RowNumber, source.Values,
                        invoiceNumbers, incomingInvoiceNumbers, trackingNumbers, incomingTrackingNumbers),
                    _ => throw new ArgumentOutOfRangeException()
                };
                rows.Add(row);
            }
        }

        var batch = new LegacyImportBatch
        {
            FileName = Path.GetFileName(fileName), FileSha256 = hash, Status = LegacyImportStatus.Previewed,
            TotalRows = rows.Count, ValidRows = rows.Count(x => x.Status == LegacyImportRowStatus.Valid),
            DuplicateRows = rows.Count(x => x.Status == LegacyImportRowStatus.Duplicate),
            ErrorRows = rows.Count(x => x.Status == LegacyImportRowStatus.Error),
            CreatedBy = actorUserId.ToString()
        };
        foreach (var row in rows) row.LegacyImportBatchId = batch.Id;
        _context.LegacyImportBatches.Add(batch);
        _context.LegacyImportRows.AddRange(rows);
        await _context.SaveChangesAsync(cancellationToken);
        return await ResultAsync(batch, cancellationToken);
    }

    public async Task<LegacyImportCommitResult> CommitAsync(Guid batchId, Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable,
                cancellationToken);
            var batch = await _context.LegacyImportBatches.FirstOrDefaultAsync(x => x.Id == batchId,
                cancellationToken) ?? throw new KeyNotFoundException("Legacy import batch not found.");
            if (batch.Status is LegacyImportStatus.Completed or LegacyImportStatus.CompletedWithErrors)
                return new LegacyImportCommitResult(batch.Id, batch.Status, batch.ImportedRows,
                    batch.DuplicateRows, batch.ErrorRows);
            if (batch.Status != LegacyImportStatus.Previewed)
                throw new InvalidOperationException("Legacy import is not ready to commit.");
            batch.Status = LegacyImportStatus.Committing;
            await _context.SaveChangesAsync(cancellationToken);
            var rows = await _context.LegacyImportRows.Where(x => x.LegacyImportBatchId == batchId)
                .OrderBy(x => x.EntityType).ThenBy(x => x.RowNumber).ToListAsync(cancellationToken);
            var customerMap = await _context.Customers.ToListAsync(cancellationToken);
            var invoiceMap = await _context.Invoices.ToDictionaryAsync(x => Normalize(x.InvoiceNumber),
                cancellationToken);
            var trackingMap = await _context.CargoItems.ToDictionaryAsync(x => Normalize(x.TrackingNumber),
                cancellationToken);
            var branchMap = await _context.Branches.AsNoTracking().Where(x => x.IsActive)
                .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var imported = 0;

            foreach (var row in rows.Where(x => x.EntityType == LegacyImportEntityType.Client))
            {
                if (row.Status == LegacyImportRowStatus.Error) continue;
                var data = Deserialize<LegacyClientPayload>(row);
                var match = MatchCustomer(customerMap, data.FullName, data.Email, data.Phone);
                if (match.Conflict)
                { MarkError(row, "Client identifiers match different existing records."); continue; }
                if (match.Customer is not null)
                { MarkSkipped(row, match.Customer.Id, "Linked to existing client."); continue; }
                var customer = new Customer(Guid.NewGuid(), data.FullName, data.Email, 0, data.CountryCode)
                { Phone = data.Phone, WalletCurrency = CountryCurrency.ForCountry(data.CountryCode) };
                _context.Customers.Add(customer); customerMap.Add(customer);
                MarkImported(row, customer.Id); imported++;
            }
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var row in rows.Where(x => x.EntityType == LegacyImportEntityType.Invoice))
            {
                if (row.Status == LegacyImportRowStatus.Error) continue;
                var data = Deserialize<LegacyInvoicePayload>(row);
                var key = Normalize(data.InvoiceNumber);
                if (invoiceMap.TryGetValue(key, out var existingInvoice))
                { MarkSkipped(row, existingInvoice.Id, "Invoice number already exists."); continue; }
                var match = MatchCustomer(customerMap, data.ClientName, data.ClientEmail, data.ClientPhone);
                if (match.Conflict || match.Customer is null)
                { MarkError(row, "A unique client could not be resolved for this invoice."); continue; }
                if (!branchMap.TryGetValue(data.IssuerBranchCode, out var branch))
                { MarkError(row, "Issuer branch is not active."); continue; }
                Invoice? original = null;
                if (data.Type == InvoiceType.CreditNote)
                {
                    if (string.IsNullOrWhiteSpace(data.OriginalInvoiceNumber) ||
                        !invoiceMap.TryGetValue(Normalize(data.OriginalInvoiceNumber), out original))
                    { MarkError(row, "Credit note original invoice was not imported or found."); continue; }
                    if (original.CustomerId != match.Customer.Id)
                    { MarkError(row, "Credit note and original invoice clients differ."); continue; }
                }
                var paid = Math.Clamp(data.AmountPaid, 0, data.Amount);
                var invoice = new Invoice
                {
                    CustomerId = match.Customer.Id, CustomerName = match.Customer.FullName,
                    InvoiceNumber = data.InvoiceNumber.Trim(), Type = data.Type,
                    OriginalInvoiceId = original?.Id, IssuerBranchCode = branch.Code,
                    IssueDate = data.IssueDate.Date, DueDate = data.DueDate.Date,
                    Currency = CountryCurrency.ForCountry(branch.CountryCode), Subtotal = data.Amount,
                    TotalAmount = data.Amount, AmountPaid = data.Type == InvoiceType.CreditNote ? 0 : paid,
                    Status = data.Type == InvoiceType.CreditNote ? InvoiceStatus.Issued :
                        paid >= data.Amount ? InvoiceStatus.Paid : paid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Issued,
                    PaymentStatus = data.Type == InvoiceType.CreditNote ? InvoicePaymentStatus.NotApplicable :
                        paid >= data.Amount ? InvoicePaymentStatus.Paid : paid > 0 ? InvoicePaymentStatus.PartiallyPaid :
                            InvoicePaymentStatus.Unpaid,
                    Notes = string.IsNullOrWhiteSpace(data.Notes) ? "Imported from the legacy workbook." : data.Notes,
                    PreferredPaymentMethod = data.Type == InvoiceType.CreditNote ? null : PaymentMethod.BankTransfer,
                    VerificationCode = DocumentSecurity.NewVerificationCode()
                };
                var line = new InvoiceLine { InvoiceId = invoice.Id, Description = "Legacy invoice balance",
                    Quantity = 1, UnitPrice = data.Amount, TaxRate = 0, LineTotal = data.Amount };
                _context.Invoices.Add(invoice); _context.InvoiceLines.Add(line); invoiceMap[key] = invoice;
                MarkImported(row, invoice.Id); imported++;
            }
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var row in rows.Where(x => x.EntityType == LegacyImportEntityType.Cargo))
            {
                if (row.Status == LegacyImportRowStatus.Error) continue;
                var data = Deserialize<LegacyCargoPayload>(row);
                var key = Normalize(data.TrackingNumber);
                if (trackingMap.TryGetValue(key, out var existingCargo))
                { MarkSkipped(row, existingCargo.Id, "Tracking number already exists."); continue; }
                if (!invoiceMap.TryGetValue(Normalize(data.InvoiceNumber), out var invoice))
                { MarkError(row, "Cargo invoice was not imported or found."); continue; }
                var customer = customerMap.Single(x => x.Id == invoice.CustomerId);
                var cargo = new CargoItem(Guid.NewGuid(), customer.Id, data.TrackingNumber.Trim(),
                    data.Description, data.ActualWeightKg, data.LengthCm, data.WidthCm, data.HeightCm,
                    data.DeclaredValue, string.IsNullOrWhiteSpace(data.Currency) ? invoice.Currency : data.Currency,
                    data.CustomsCategory, customer.CountryCode)
                { InvoiceId = invoice.Id, Status = data.Status, ShippingCostCurrency = invoice.Currency };
                _context.CargoItems.Add(cargo); trackingMap[key] = cargo;
                MarkImported(row, cargo.Id); imported++;
            }
            await _context.SaveChangesAsync(cancellationToken);

            batch.ImportedRows = imported;
            batch.ErrorRows = rows.Count(x => x.Status == LegacyImportRowStatus.Error);
            batch.DuplicateRows = rows.Count(x => x.Status == LegacyImportRowStatus.Skipped);
            batch.CommittedAt = DateTime.UtcNow;
            batch.CommittedByUserId = actorUserId.ToString();
            batch.Status = batch.ErrorRows > 0 ? LegacyImportStatus.CompletedWithErrors : LegacyImportStatus.Completed;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new LegacyImportCommitResult(batch.Id, batch.Status, batch.ImportedRows,
                batch.DuplicateRows, batch.ErrorRows);
        });
    }

    private async Task<LegacyImportPreviewResult> ResultAsync(LegacyImportBatch batch,
        CancellationToken cancellationToken)
    {
        var counts = await _context.LegacyImportRows.AsNoTracking().Where(x => x.LegacyImportBatchId == batch.Id)
            .GroupBy(x => x.EntityType).Select(x => new { Type = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);
        var samples = await _context.LegacyImportRows.AsNoTracking().Where(x => x.LegacyImportBatchId == batch.Id)
            .OrderBy(x => x.EntityType).ThenBy(x => x.RowNumber).Take(100).ToListAsync(cancellationToken);
        return new LegacyImportPreviewResult(batch, counts, samples);
    }

    private static LegacyImportEntityType DetectType(LegacyWorkbookSheet sheet)
    {
        var name = LegacyWorkbookReader.NormalizeHeader(sheet.Name);
        if (name.Contains("client") || name.Contains("customer")) return LegacyImportEntityType.Client;
        if (name.Contains("invoice") || name.Contains("creditnote")) return LegacyImportEntityType.Invoice;
        if (name.Contains("cargo") || name.Contains("shipment") || name.Contains("item"))
            return LegacyImportEntityType.Cargo;
        var headers = sheet.Rows.FirstOrDefault().Values?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>();
        if (headers.Any(x => x.Contains("tracking"))) return LegacyImportEntityType.Cargo;
        if (headers.Any(x => x.Contains("invoice"))) return LegacyImportEntityType.Invoice;
        if (headers.Any(x => x.Contains("email")) && headers.Any(x => x.Contains("phone") || x.Contains("mobile")))
            return LegacyImportEntityType.Client;
        throw new ArgumentException($"Worksheet '{sheet.Name}' could not be classified. Rename it Clients, Invoices, or Cargo.");
    }

    private static LegacyImportRow PreviewClient(string sheet, int rowNumber,
        IReadOnlyDictionary<string, string> values, IReadOnlyList<ExistingCustomer> existing,
        ICollection<LegacyClientPayload> incoming)
    {
        var data = new LegacyClientPayload(V(values, "Full Name", "Name", "Customer Name", "Client Name"),
            V(values, "Email", "Email Address"), V(values, "Phone", "Phone Number", "Mobile", "Number"),
            V(values, "Country Code", "Country"));
        var errors = Required((data.FullName, "full name"), (data.Email, "email"),
            (data.Phone, "phone"), (data.CountryCode, "country code"));
        if (!data.Email.Contains('@')) errors.Add("email is invalid");
        if (data.CountryCode.Length is < 2 or > 3) errors.Add("country code must use ISO alpha-2 or alpha-3");
        var match = MatchExisting(existing, data.FullName, data.Email, data.Phone);
        var incomingMatch = incoming.Any(x => SameIdentity(x, data));
        LegacyImportRowStatus status; string? message;
        if (errors.Count > 0) { status = LegacyImportRowStatus.Error; message = string.Join("; ", errors); }
        else if (match.Conflict) { status = LegacyImportRowStatus.Error; message = "identifiers match different clients"; }
        else if (match.Id.HasValue) { status = LegacyImportRowStatus.Duplicate; message = $"will link existing client {match.Id}"; }
        else if (incomingMatch) { status = LegacyImportRowStatus.Duplicate; message = "duplicate client inside workbook"; }
        else { status = LegacyImportRowStatus.Valid; message = null; incoming.Add(data); }
        return Row(sheet, rowNumber, LegacyImportEntityType.Client, status,
            data.Email.Length > 0 ? data.Email : data.FullName, data, message);
    }

    private static LegacyImportRow PreviewInvoice(string sheet, int rowNumber,
        IReadOnlyDictionary<string, string> values, IReadOnlyList<ExistingCustomer> customers,
        IReadOnlyCollection<LegacyClientPayload> incomingClients, IReadOnlyDictionary<string, Guid> invoices,
        ISet<string> incomingInvoices, IReadOnlyDictionary<string, Branch> branches)
    {
        var typeText = V(values, "Invoice Type", "Type");
        var type = typeText.Contains("credit", StringComparison.OrdinalIgnoreCase)
            ? InvoiceType.CreditNote : InvoiceType.Sales;
        var paymentText = V(values, "Payment Status", "Status");
        var paymentStatus = paymentText.Contains("partial", StringComparison.OrdinalIgnoreCase)
            ? InvoicePaymentStatus.PartiallyPaid : paymentText.Contains("paid", StringComparison.OrdinalIgnoreCase)
                ? InvoicePaymentStatus.Paid : InvoicePaymentStatus.Unpaid;
        var amount = LegacyWorkbookReader.Decimal(V(values, "Amount", "Total", "Invoice Amount")) ?? 0;
        var amountPaid = LegacyWorkbookReader.Decimal(V(values, "Amount Paid", "Paid Amount")) ??
            (paymentStatus == InvoicePaymentStatus.Paid ? amount : 0);
        var issueDate = LegacyWorkbookReader.Date(V(values, "Issue Date", "Invoice Date", "Date"));
        var dueDate = LegacyWorkbookReader.Date(V(values, "Due Date")) ?? issueDate;
        var data = new LegacyInvoicePayload(V(values, "Invoice Number", "Invoice No", "Invoice #"),
            V(values, "Client Name", "Customer Name", "Name"), V(values, "Client Email", "Customer Email", "Email"),
            V(values, "Client Phone", "Customer Phone", "Phone", "Mobile"), type,
            V(values, "Original Invoice Number", "Original Invoice", "Related Invoice"),
            V(values, "Issuer Branch Code", "Branch Code", "Issuer"), issueDate ?? default,
            dueDate ?? default, paymentStatus, amount, amountPaid, V(values, "Notes", "Description"));
        var errors = Required((data.InvoiceNumber, "invoice number"), (data.IssuerBranchCode, "issuer branch code"));
        if (!issueDate.HasValue) errors.Add("issue date is invalid or missing");
        if (!issueDate.HasValue || !dueDate.HasValue || dueDate.Value.Date < issueDate.Value.Date)
            errors.Add("due date is invalid");
        if (amount <= 0) errors.Add("amount must be greater than zero");
        if (amountPaid < 0 || amountPaid > amount) errors.Add("amount paid must be between zero and amount");
        if (!branches.ContainsKey(data.IssuerBranchCode)) errors.Add("issuer branch is not active");
        if (type == InvoiceType.CreditNote && string.IsNullOrWhiteSpace(data.OriginalInvoiceNumber))
            errors.Add("credit note requires original invoice number");
        var hasClient = MatchExisting(customers, data.ClientName, data.ClientEmail, data.ClientPhone).Id.HasValue ||
            incomingClients.Any(x => Matches(x, data.ClientName, data.ClientEmail, data.ClientPhone));
        if (!hasClient) errors.Add("client was not found in existing data or the Clients sheet");
        var key = Normalize(data.InvoiceNumber);
        LegacyImportRowStatus status; string? message;
        if (errors.Count > 0) { status = LegacyImportRowStatus.Error; message = string.Join("; ", errors); }
        else if (invoices.ContainsKey(key)) { status = LegacyImportRowStatus.Duplicate; message = "invoice already exists"; }
        else if (!incomingInvoices.Add(key)) { status = LegacyImportRowStatus.Duplicate; message = "duplicate invoice inside workbook"; }
        else { status = LegacyImportRowStatus.Valid; message = null; }
        return Row(sheet, rowNumber, LegacyImportEntityType.Invoice, status, data.InvoiceNumber, data, message);
    }

    private static LegacyImportRow PreviewCargo(string sheet, int rowNumber,
        IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, Guid> invoices,
        IReadOnlySet<string> incomingInvoices, IReadOnlyDictionary<string, Guid> cargo,
        ISet<string> incomingCargo)
    {
        var rawStatus = V(values, "Cargo Status", "Status");
        var statusText = rawStatus.Replace(" ", string.Empty);
        var statusValid = string.IsNullOrWhiteSpace(statusText) ||
            Enum.TryParse<CargoStatus>(statusText, true, out _);
        var status = Enum.TryParse<CargoStatus>(statusText, true, out var parsed) ? parsed : CargoStatus.Logged;
        var data = new LegacyCargoPayload(V(values, "Tracking Number", "Tracking No", "Tracking #"),
            V(values, "Invoice Number", "Invoice No", "Invoice #"), V(values, "Description", "Item Description"),
            LegacyWorkbookReader.Decimal(V(values, "Weight Kg", "Actual Weight Kg", "Weight")) ?? 0,
            LegacyWorkbookReader.Decimal(V(values, "Length Cm", "Length")) ?? 1,
            LegacyWorkbookReader.Decimal(V(values, "Width Cm", "Width")) ?? 1,
            LegacyWorkbookReader.Decimal(V(values, "Height Cm", "Height")) ?? 1,
            LegacyWorkbookReader.Decimal(V(values, "Declared Value", "Value")) ?? 0,
            V(values, "Currency", "Declared Value Currency"),
            V(values, "Customs Category", "Category") is { Length: > 0 } category ? category : "General Cargo",
            status);
        var errors = Required((data.TrackingNumber, "tracking number"),
            (data.InvoiceNumber, "invoice number"), (data.Description, "description"));
        if (data.ActualWeightKg <= 0) errors.Add("weight must be greater than zero");
        if (!statusValid) errors.Add("cargo status is not recognized");
        var invoiceKey = Normalize(data.InvoiceNumber);
        if (!invoices.ContainsKey(invoiceKey) && !incomingInvoices.Contains(invoiceKey))
            errors.Add("invoice was not found in existing data or the Invoices sheet");
        var key = Normalize(data.TrackingNumber);
        LegacyImportRowStatus rowStatus; string? message;
        if (errors.Count > 0) { rowStatus = LegacyImportRowStatus.Error; message = string.Join("; ", errors); }
        else if (cargo.ContainsKey(key)) { rowStatus = LegacyImportRowStatus.Duplicate; message = "tracking number already exists"; }
        else if (!incomingCargo.Add(key)) { rowStatus = LegacyImportRowStatus.Duplicate; message = "duplicate cargo inside workbook"; }
        else { rowStatus = LegacyImportRowStatus.Valid; message = null; }
        return Row(sheet, rowNumber, LegacyImportEntityType.Cargo, rowStatus, data.TrackingNumber, data, message);
    }

    private static LegacyImportRow Row<T>(string sheet, int row, LegacyImportEntityType type,
        LegacyImportRowStatus status, string sourceKey, T data, string? message) => new()
    {
        SheetName = sheet, RowNumber = row, EntityType = type, Status = status,
        SourceKey = sourceKey.Trim(), DataJson = JsonSerializer.Serialize(data, JsonOptions), Message = message
    };

    private static T Deserialize<T>(LegacyImportRow row) => JsonSerializer.Deserialize<T>(row.DataJson, JsonOptions)
        ?? throw new InvalidOperationException($"Legacy import row {row.Id} is invalid.");
    private static string V(IReadOnlyDictionary<string, string> row, params string[] aliases) =>
        LegacyWorkbookReader.Value(row, aliases);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static List<string> Required(params (string Value, string Name)[] values) => values
        .Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{x.Name} is required").ToList();

    private static (Guid? Id, bool Conflict) MatchExisting(IReadOnlyList<ExistingCustomer> customers,
        string name, string email, string phone)
    {
        var ids = customers.Where(x =>
                (!string.IsNullOrWhiteSpace(name) && x.Name == ClientIdentityNormalizer.Name(name)) ||
                (!string.IsNullOrWhiteSpace(email) && x.Email == ClientIdentityNormalizer.Email(email)) ||
                (!string.IsNullOrWhiteSpace(phone) && x.Phone == ClientIdentityNormalizer.Phone(phone)))
            .Select(x => x.Id).Distinct().Take(2).ToList();
        return (ids.Count == 1 ? ids[0] : null, ids.Count > 1);
    }

    private static (Customer? Customer, bool Conflict) MatchCustomer(IReadOnlyList<Customer> customers,
        string name, string email, string phone)
    {
        var matches = customers.Where(x =>
                (!string.IsNullOrWhiteSpace(name) && x.NormalizedFullName == ClientIdentityNormalizer.Name(name)) ||
                (!string.IsNullOrWhiteSpace(email) && x.NormalizedEmail == ClientIdentityNormalizer.Email(email)) ||
                (!string.IsNullOrWhiteSpace(phone) && x.NormalizedPhone == ClientIdentityNormalizer.Phone(phone)))
            .DistinctBy(x => x.Id).Take(2).ToList();
        return (matches.Count == 1 ? matches[0] : null, matches.Count > 1);
    }

    private static bool SameIdentity(LegacyClientPayload left, LegacyClientPayload right) =>
        Matches(left, right.FullName, right.Email, right.Phone);
    private static bool Matches(LegacyClientPayload client, string name, string email, string phone) =>
        (!string.IsNullOrWhiteSpace(name) && ClientIdentityNormalizer.Name(client.FullName) == ClientIdentityNormalizer.Name(name)) ||
        (!string.IsNullOrWhiteSpace(email) && ClientIdentityNormalizer.Email(client.Email) == ClientIdentityNormalizer.Email(email)) ||
        (!string.IsNullOrWhiteSpace(phone) && ClientIdentityNormalizer.Phone(client.Phone) == ClientIdentityNormalizer.Phone(phone));

    private static void MarkImported(LegacyImportRow row, Guid id)
    { row.Status = LegacyImportRowStatus.Imported; row.ImportedEntityId = id; row.ProcessedAt = DateTime.UtcNow; }
    private static void MarkSkipped(LegacyImportRow row, Guid id, string message)
    { row.Status = LegacyImportRowStatus.Skipped; row.ImportedEntityId = id; row.Message = message; row.ProcessedAt = DateTime.UtcNow; }
    private static void MarkError(LegacyImportRow row, string message)
    { row.Status = LegacyImportRowStatus.Error; row.Message = message; row.ProcessedAt = DateTime.UtcNow; }

    private sealed record ExistingCustomer(Guid Id, string Name, string Email, string? Phone);
    private sealed record LegacyClientPayload(string FullName, string Email, string Phone, string CountryCode);
    private sealed record LegacyInvoicePayload(string InvoiceNumber, string ClientName, string ClientEmail,
        string ClientPhone, InvoiceType Type, string OriginalInvoiceNumber, string IssuerBranchCode,
        DateTime IssueDate, DateTime DueDate, InvoicePaymentStatus PaymentStatus,
        decimal Amount, decimal AmountPaid, string Notes);
    private sealed record LegacyCargoPayload(string TrackingNumber, string InvoiceNumber, string Description,
        decimal ActualWeightKg, decimal LengthCm, decimal WidthCm, decimal HeightCm,
        decimal DeclaredValue, string Currency, string CustomsCategory, CargoStatus Status);
}

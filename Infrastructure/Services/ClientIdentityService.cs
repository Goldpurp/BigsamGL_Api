using System.Text.Json;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ClientIdentityService : IClientIdentityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _context;
    public ClientIdentityService(IApplicationDbContext context) => _context = context;

    public Task<ClientDuplicateReviewResult> CheckAsync(ClientIdentityInput input, Guid? excludeCustomerId,
        CancellationToken cancellationToken) => EvaluateAsync(input, excludeCustomerId, true, cancellationToken);

    public async Task<ClientDuplicateReviewResult?> ValidateNewClientAsync(ClientIdentityInput input,
        Guid? reviewId, Guid? excludeCustomerId, CancellationToken cancellationToken)
    {
        var fingerprint = Fingerprint(input);
        if (reviewId.HasValue)
        {
            var approved = await _context.ClientDuplicateReviews.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == reviewId && x.InputFingerprint == fingerprint && x.ExpiresAt > DateTime.UtcNow,
                cancellationToken);
            if (approved?.Status == ClientDuplicateReviewStatus.NewClientApproved && !approved.HasExactConflict &&
                !await HasExactConflictAsync(input, excludeCustomerId, cancellationToken))
                return null;
            if (approved?.Status == ClientDuplicateReviewStatus.ExistingClientSelected)
                return Map(approved);
        }

        var result = await EvaluateAsync(input, excludeCustomerId, false, cancellationToken);
        return result.Matches.Count == 0 ? null : result;
    }

    public async Task<Guid?> GetSelectedExistingClientAsync(ClientIdentityInput input, Guid? reviewId,
        CancellationToken cancellationToken)
    {
        if (!reviewId.HasValue) return null;
        var fingerprint = Fingerprint(input);
        return await _context.ClientDuplicateReviews.AsNoTracking().Where(x => x.Id == reviewId &&
                x.InputFingerprint == fingerprint && x.ExpiresAt > DateTime.UtcNow &&
                x.Status == ClientDuplicateReviewStatus.ExistingClientSelected)
            .Select(x => x.SelectedCustomerId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ClientDuplicateReviewResult> ResolveAsync(Guid reviewId, ClientDuplicateDecision decision,
        Guid? selectedCustomerId, string reason, CancellationToken cancellationToken)
    {
        var review = await _context.ClientDuplicateReviews.FirstOrDefaultAsync(x => x.Id == reviewId,
            cancellationToken) ?? throw new KeyNotFoundException("Duplicate review not found.");
        if (review.ExpiresAt <= DateTime.UtcNow)
        {
            review.Status = ClientDuplicateReviewStatus.Expired;
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This duplicate review has expired. Run the match check again.");
        }
        if (review.Status != ClientDuplicateReviewStatus.Pending)
            throw new InvalidOperationException("This duplicate review has already been resolved.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A decision reason is required for the audit history.");

        var candidates = Deserialize(review.MatchSummaryJson);
        switch (decision)
        {
            case ClientDuplicateDecision.UseExistingClient:
                if (!selectedCustomerId.HasValue || candidates.All(x => x.CustomerId != selectedCustomerId.Value))
                    throw new ArgumentException("Select one of the suggested existing clients.");
                review.Status = ClientDuplicateReviewStatus.ExistingClientSelected;
                review.SelectedCustomerId = selectedCustomerId;
                break;
            case ClientDuplicateDecision.CreateNewClient:
                if (review.HasExactConflict)
                    throw new InvalidOperationException("An exact client identity match cannot be ignored. Use the existing client.");
                review.Status = ClientDuplicateReviewStatus.NewClientApproved;
                break;
            case ClientDuplicateDecision.Cancel:
                review.Status = ClientDuplicateReviewStatus.Cancelled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision));
        }
        review.DecisionReason = reason.Trim(); review.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Map(review);
    }

    private async Task<ClientDuplicateReviewResult> EvaluateAsync(ClientIdentityInput input,
        Guid? excludeCustomerId, bool persistWhenClear, CancellationToken cancellationToken)
    {
        var normalizedName = ClientIdentityNormalizer.Name(input.FullName);
        var normalizedEmail = ClientIdentityNormalizer.Email(input.Email);
        var normalizedPhone = ClientIdentityNormalizer.Phone(input.Phone);
        var invoiceNumber = ClientIdentityNormalizer.Invoice(input.InvoiceNumber);
        if (string.IsNullOrWhiteSpace(normalizedName)) throw new ArgumentException("Client name is required.");

        var candidates = new Dictionary<Guid, Customer>();
        var exact = await _context.Customers.AsNoTracking().Where(x =>
                x.MergedIntoCustomerId == null &&
                (!excludeCustomerId.HasValue || x.Id != excludeCustomerId) &&
                (x.NormalizedFullName == normalizedName ||
                 (normalizedEmail.Length > 0 && x.NormalizedEmail == normalizedEmail) ||
                 (normalizedPhone != null && x.NormalizedPhone == normalizedPhone)))
            .OrderBy(x => x.Id).Take(30).ToListAsync(cancellationToken);
        Add(candidates, exact);

        var firstNameToken = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? normalizedName;
        var nameCandidates = await _context.Customers.AsNoTracking().Where(x =>
                x.MergedIntoCustomerId == null &&
                (!excludeCustomerId.HasValue || x.Id != excludeCustomerId) &&
                x.NormalizedFullName.StartsWith(firstNameToken))
            .OrderBy(x => x.Id).Take(50).ToListAsync(cancellationToken);
        Add(candidates, nameCandidates);

        if (normalizedPhone is { Length: >= 7 })
        {
            var phoneTail = normalizedPhone[^7..];
            Add(candidates, await _context.Customers.AsNoTracking().Where(x =>
                    x.MergedIntoCustomerId == null &&
                    (!excludeCustomerId.HasValue || x.Id != excludeCustomerId) &&
                    x.NormalizedPhone != null && x.NormalizedPhone.EndsWith(phoneTail))
                .OrderBy(x => x.Id).Take(30).ToListAsync(cancellationToken));
        }

        var at = normalizedEmail.LastIndexOf('@');
        if (at > 2)
        {
            var emailPrefix = normalizedEmail[..Math.Min(3, at)];
            Add(candidates, await _context.Customers.AsNoTracking().Where(x =>
                    x.MergedIntoCustomerId == null &&
                    (!excludeCustomerId.HasValue || x.Id != excludeCustomerId) &&
                    x.NormalizedEmail != null && x.NormalizedEmail.StartsWith(emailPrefix))
                .OrderBy(x => x.Id).Take(30).ToListAsync(cancellationToken));
        }

        var invoiceOwners = new HashSet<Guid>();
        if (invoiceNumber.Length > 0)
        {
            var financeOwner = await _context.Invoices.AsNoTracking().Where(x => x.InvoiceNumber == invoiceNumber)
                .Select(x => (Guid?)x.CustomerId).FirstOrDefaultAsync(cancellationToken);
            if (financeOwner.HasValue) invoiceOwners.Add(financeOwner.Value);
            var shipmentOwner = await _context.CustomerShipments.AsNoTracking()
                .Where(x => x.InvoiceNumber == invoiceNumber)
                .Select(x => (Guid?)x.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
            if (shipmentOwner.HasValue) invoiceOwners.Add(shipmentOwner.Value);
            var invoiceCustomers = await _context.Customers.AsNoTracking().Where(x =>
                x.MergedIntoCustomerId == null && invoiceOwners.Contains(x.Id) &&
                (!excludeCustomerId.HasValue || x.Id != excludeCustomerId)).ToListAsync(cancellationToken);
            Add(candidates, invoiceCustomers);
        }

        var matches = new List<ClientMatchCandidate>();
        foreach (var customer in candidates.Values)
        {
            var fields = new List<string>(); var exactMatch = false; var score = 0m;
            if (customer.NormalizedFullName == normalizedName) { fields.Add("FullName"); exactMatch = true; score += 45; }
            else
            {
                var similarity = Similarity(customer.NormalizedFullName, normalizedName);
                if (similarity >= .82m) { fields.Add("SimilarFullName"); score += similarity * 35; }
            }
            if (normalizedEmail.Length > 0 && customer.NormalizedEmail == normalizedEmail) { fields.Add("Email"); exactMatch = true; score += 45; }
            else if (normalizedEmail.Length > 0 && Similarity(customer.NormalizedEmail ?? string.Empty, normalizedEmail) >= .90m)
            { fields.Add("SimilarEmail"); score += 25; }
            if (normalizedPhone != null && customer.NormalizedPhone == normalizedPhone)
            { fields.Add("Phone"); exactMatch = true; score += 45; }
            else if (normalizedPhone is { Length: >= 7 } && customer.NormalizedPhone?.EndsWith(normalizedPhone[^7..]) == true)
            { fields.Add("SimilarPhone"); score += 25; }
            if (invoiceOwners.Contains(customer.Id)) { fields.Add("InvoiceNumber"); exactMatch = true; score += 50; }
            if (fields.Count == 0) continue;
            matches.Add(new ClientMatchCandidate(customer.Id, customer.FullName, customer.Email, customer.Phone,
                fields, Math.Min(100, Math.Round(score, 2)), exactMatch));
        }
        matches = matches.OrderByDescending(x => x.IsExact).ThenByDescending(x => x.SimilarityScore).Take(20).ToList();
        var hasExact = matches.Any(x => x.IsExact);
        var review = new ClientDuplicateReview
        {
            InputFingerprint = Fingerprint(input), SubmittedFullName = input.FullName.Trim(),
            SubmittedEmail = input.Email?.Trim().ToLowerInvariant() ?? string.Empty, SubmittedPhone = input.Phone.Trim(),
            SubmittedInvoiceNumber = invoiceNumber, MatchSummaryJson = JsonSerializer.Serialize(matches, JsonOptions),
            HasExactConflict = hasExact, ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        if (matches.Count > 0 || persistWhenClear)
        {
            _context.ClientDuplicateReviews.Add(review);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return Build(review, matches);
    }

    private static void Add(IDictionary<Guid, Customer> target, IEnumerable<Customer> values)
    { foreach (var value in values) target[value.Id] = value; }

    private async Task<bool> HasExactConflictAsync(ClientIdentityInput input, Guid? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        var name = ClientIdentityNormalizer.Name(input.FullName);
        var email = ClientIdentityNormalizer.Email(input.Email);
        var phone = ClientIdentityNormalizer.Phone(input.Phone);
        if (await _context.Customers.AsNoTracking().AnyAsync(x =>
                x.MergedIntoCustomerId == null &&
                (!excludeCustomerId.HasValue || x.Id != excludeCustomerId) &&
                (x.NormalizedFullName == name || (email.Length > 0 && x.NormalizedEmail == email) ||
                 (phone != null && x.NormalizedPhone == phone)), cancellationToken)) return true;
        var invoice = ClientIdentityNormalizer.Invoice(input.InvoiceNumber);
        if (invoice.Length == 0) return false;
        if (await _context.Invoices.AsNoTracking().AnyAsync(x => x.InvoiceNumber == invoice &&
            (!excludeCustomerId.HasValue || x.CustomerId != excludeCustomerId), cancellationToken)) return true;
        return await _context.CustomerShipments.AsNoTracking().AnyAsync(x =>
            x.InvoiceNumber == invoice && (!excludeCustomerId.HasValue || x.CustomerId != excludeCustomerId),
            cancellationToken);
    }

    private static string Fingerprint(ClientIdentityInput input) => ClientIdentityNormalizer.Fingerprint(
        input.FullName, input.Email, input.Phone, input.InvoiceNumber);

    private static ClientDuplicateReviewResult Map(ClientDuplicateReview review) =>
        Build(review, Deserialize(review.MatchSummaryJson));

    private static ClientDuplicateReviewResult Build(ClientDuplicateReview review,
        IReadOnlyList<ClientMatchCandidate> matches)
    {
        var action = matches.Count == 0 ? "No existing client matched. Creation may continue."
            : review.HasExactConflict ? "Use the matching existing client; exact identity details cannot be duplicated."
            : "Confirm the existing client or approve a new client with an audit reason.";
        return new ClientDuplicateReviewResult(review.Id, matches.Count > 0,
            matches.Count == 0 || !review.HasExactConflict, review.HasExactConflict, review.SelectedCustomerId,
            review.ExpiresAt, action, matches);
    }

    private static List<ClientMatchCandidate> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<ClientMatchCandidate>>(json, JsonOptions) ?? new();

    private static decimal Similarity(string left, string right)
    {
        if (left == right) return 1;
        if (left.Length == 0 || right.Length == 0) return 0;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1]; current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            previous = current;
        }
        return Math.Round(1m - previous[^1] / (decimal)Math.Max(left.Length, right.Length), 4);
    }
}

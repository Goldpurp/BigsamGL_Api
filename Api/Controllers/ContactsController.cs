using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/contacts")]
public sealed class ContactsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public ContactsController(IApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<ActionResult> Create(CreateContactRequest request, CancellationToken cancellationToken)
    {
        if (IsCorporateType(request.Type) && !CanManageCorporateContacts()) return Forbid();
        var error = await ValidateAsync(request.Type, request.FullName, request.Email, request.Phone,
            request.CountryCode, request.CustomerId, request.SupplierId,
            request.PreferredBranchId, request.OwnerUserId, cancellationToken);
        if (error is not null) return BadRequest(error);
        var matches = await FindMatchesAsync(request.FullName, request.Email, request.Phone, null,
            cancellationToken);
        if (matches.Count > 0 && !request.IgnorePotentialDuplicate)
            return Conflict(new { Message = "This contact resembles an existing record. Review the suggestions or explicitly confirm creation.",
                PotentialMatches = matches, CanOverride = true });
        var contact = new Contact();
        Apply(contact, request.Type, ContactStatus.Active, request.FullName, request.CompanyName,
            request.JobTitle, request.Email, request.Phone, request.CountryCode, request.Address,
            request.Tags, request.Notes, request.CustomerId, request.SupplierId,
            request.PreferredBranchId, request.OwnerUserId, request.IsPrimary);
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = contact.Id }, contact);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, UpdateContactRequest request,
        CancellationToken cancellationToken)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Contact not found.");
        if ((IsCorporateType(contact.Type) || IsCorporateType(request.Type)) && !CanManageCorporateContacts())
            return Forbid();
        var error = await ValidateAsync(request.Type, request.FullName, request.Email, request.Phone,
            request.CountryCode, request.CustomerId, request.SupplierId,
            request.PreferredBranchId, request.OwnerUserId, cancellationToken);
        if (error is not null) return BadRequest(error);
        var matches = await FindMatchesAsync(request.FullName, request.Email, request.Phone, id,
            cancellationToken);
        if (matches.Count > 0 && !request.IgnorePotentialDuplicate)
            return Conflict(new { Message = "Updated details resemble another contact. Review the suggestions or explicitly confirm the update.",
                PotentialMatches = matches, CanOverride = true });
        Apply(contact, request.Type, request.Status, request.FullName, request.CompanyName,
            request.JobTitle, request.Email, request.Phone, request.CountryCode, request.Address,
            request.Tags, request.Notes, request.CustomerId, request.SupplierId,
            request.PreferredBranchId, request.OwnerUserId, request.IsPrimary);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(contact);
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] ContactType? type,
        [FromQuery] Guid? customerId, [FromQuery] Guid? supplierId,
        [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        if (type.HasValue && IsRetiredDirectoryType(type.Value))
            return StatusCode(StatusCodes.Status410Gone,
                new { Code = "CONTACT_CATEGORY_RETIRED", Message = "Use /api/clients for client records." });
        if (type.HasValue && IsCorporateType(type.Value) && !CanManageCorporateContacts()) return Forbid();
        var query = _context.Contacts.AsNoTracking().Where(x =>
            x.Type != ContactType.Client && x.Type != ContactType.Prospect &&
            x.Type != ContactType.Shareholder && x.Type != ContactType.ServiceProvider);
        if (!CanManageCorporateContacts())
            query = query.Where(x => x.Type != ContactType.Director && x.Type != ContactType.Shareholder);
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FullName.ToLower().Contains(value) || x.Email.ToLower().Contains(value) ||
                x.Phone.ToLower().Contains(value) || (x.CompanyName != null && x.CompanyName.ToLower().Contains(value)) ||
                (x.Tags != null && x.Tags.ToLower().Contains(value)));
        }
        return Ok(await query.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    [HttpGet("clients")]
    public Task<ActionResult> Clients([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default) =>
        ListCategory(ContactType.Client, search, page, pageSize, includeTotal, cancellationToken);

    [HttpGet("suppliers")]
    public Task<ActionResult> Suppliers([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default) =>
        ListCategory(ContactType.Supplier, search, page, pageSize, includeTotal, cancellationToken);

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("directors")]
    public Task<ActionResult> Directors([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default) =>
        ListCategory(ContactType.Director, search, page, pageSize, includeTotal, cancellationToken);

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("shareholders")]
    public Task<ActionResult> Shareholders([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default) =>
        ListCategory(ContactType.Shareholder, search, page, pageSize, includeTotal, cancellationToken);

    [HttpGet("staff")]
    public Task<ActionResult> Staff([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default) =>
        ListCategory(ContactType.Staff, search, page, pageSize, includeTotal, cancellationToken);

    [HttpGet("service-providers")]
    public Task<ActionResult> ServiceProviders([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default) =>
        ListCategory(ContactType.ServiceProvider, search, page, pageSize, includeTotal, cancellationToken);

    [HttpGet("summary")]
    public async Task<ActionResult> Summary(CancellationToken cancellationToken)
    {
        var query = _context.Contacts.AsNoTracking().Where(x =>
            x.Type != ContactType.Client && x.Type != ContactType.Prospect &&
            x.Type != ContactType.Shareholder && x.Type != ContactType.ServiceProvider);
        if (!CanManageCorporateContacts())
            query = query.Where(x => x.Type != ContactType.Director && x.Type != ContactType.Shareholder);
        var rows = await query
            .GroupBy(x => x.Type).Select(x => new { Type = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        return Ok(new { Categories = rows });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var contact = await _context.Contacts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id,
            cancellationToken);
        if (contact is null) return NotFound();
        if (IsCorporateType(contact.Type) && !CanManageCorporateContacts()) return Forbid();
        var interactions = await _context.ContactInteractions.AsNoTracking()
            .Where(x => x.ContactId == id).OrderByDescending(x => x.OccurredAt).Take(20)
            .ToListAsync(cancellationToken);
        return Ok(new { Contact = contact, RecentInteractions = interactions });
    }

    [HttpGet("match-check")]
    public async Task<ActionResult> MatchCheck([FromQuery] string? fullName, [FromQuery] string? email,
        [FromQuery] string? phone, [FromQuery] Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            return BadRequest("Provide a name, email or phone number.");
        return Ok(new { PotentialMatches = await FindMatchesAsync(fullName ?? string.Empty,
            email ?? string.Empty, phone ?? string.Empty, excludeId, cancellationToken) });
    }

    [HttpPost("{id:guid}/interactions")]
    public async Task<ActionResult> AddInteraction(Guid id, AddContactInteractionRequest request,
        CancellationToken cancellationToken)
    {
        var contact = await _context.Contacts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Contact not found.");
        if (IsCorporateType(contact.Type) && !CanManageCorporateContacts()) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Details))
            return BadRequest("Interaction subject and details are required.");
        if (request.OccurredAt == default || request.OccurredAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            return BadRequest("Interaction time is invalid or in the future.");
        var interaction = new ContactInteraction
        {
            ContactId = id, Type = request.Type, Subject = request.Subject.Trim(),
            Details = request.Details.Trim(), OccurredAt = request.OccurredAt.ToUniversalTime(),
            StaffUserId = CurrentUserId
        };
        contact.LastContactedAt = interaction.OccurredAt;
        _context.ContactInteractions.Add(interaction);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/contacts/{id}/interactions/{interaction.Id}", interaction);
    }

    [HttpGet("{id:guid}/interactions")]
    public async Task<ActionResult> ListInteractions(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, [FromQuery] bool includeTotal = false,
        CancellationToken cancellationToken = default)
    {
        var contactType = await _context.Contacts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => (ContactType?)x.Type).FirstOrDefaultAsync(cancellationToken);
        if (!contactType.HasValue) return NotFound();
        if (IsCorporateType(contactType.Value) && !CanManageCorporateContacts()) return Forbid();
        return Ok(await _context.ContactInteractions.AsNoTracking().Where(x => x.ContactId == id)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    private async Task<string?> ValidateAsync(ContactType type, string name, string email, string phone,
        string countryCode, Guid? customerId, Guid? supplierId, Guid? branchId, Guid? ownerId,
        CancellationToken cancellationToken)
    {
        if (IsRetiredDirectoryType(type))
            return "This contact category is retired. Use /api/clients for client records.";
        if (string.IsNullOrWhiteSpace(name)) return "Contact name is required.";
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            return "An email address or phone number is required.";
        if (countryCode.Trim().Length is < 2 or > 5) return "A valid country code is required.";
        if (customerId.HasValue && !await _context.Customers.AnyAsync(x => x.Id == customerId, cancellationToken))
            return "Customer was not found.";
        if (supplierId.HasValue && !await _context.Suppliers.AnyAsync(x => x.Id == supplierId, cancellationToken))
            return "Supplier was not found.";
        if (branchId.HasValue && !await _context.Branches.AnyAsync(x => x.Id == branchId && x.IsActive, cancellationToken))
            return "Active branch was not found.";
        if (ownerId.HasValue && !await _context.UserAccounts.AnyAsync(x => x.Id == ownerId && x.Role != UserRole.Customer &&
            x.Status == AccountStatus.Active, cancellationToken)) return "Active staff owner was not found.";
        return null;
    }

    private async Task<ActionResult> ListCategory(ContactType type, string? search, int page,
        int pageSize, bool includeTotal, CancellationToken cancellationToken)
    {
        var query = _context.Contacts.AsNoTracking().Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FullName.ToLower().Contains(value) || x.Email.ToLower().Contains(value) ||
                x.Phone.ToLower().Contains(value) || (x.CompanyName != null && x.CompanyName.ToLower().Contains(value)));
        }
        return Ok(await query.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
    }

    private async Task<List<object>> FindMatchesAsync(string name, string email, string phone,
        Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = ClientIdentityNormalizer.Name(name);
        var normalizedEmail = ClientIdentityNormalizer.Email(email);
        var normalizedPhone = ClientIdentityNormalizer.Phone(phone);
        var query = _context.Contacts.AsNoTracking().Where(x => !excludeId.HasValue || x.Id != excludeId.Value);
        if (!CanManageCorporateContacts())
            query = query.Where(x => x.Type != ContactType.Director && x.Type != ContactType.Shareholder);
        query = query.Where(x =>
            (normalizedEmail != "" && x.NormalizedEmail == normalizedEmail) ||
            (normalizedPhone != null && x.NormalizedPhone == normalizedPhone) ||
            (normalizedName != "" && x.NormalizedFullName == normalizedName));
        return await query.OrderBy(x => x.FullName).Take(10).Select(x => (object)new
        {
            x.Id, x.FullName, x.Email, x.Phone, x.CompanyName, x.Type, x.Status,
            MatchedName = normalizedName != "" && x.NormalizedFullName == normalizedName,
            MatchedEmail = normalizedEmail != "" && x.NormalizedEmail == normalizedEmail,
            MatchedPhone = normalizedPhone != null && x.NormalizedPhone == normalizedPhone
        }).ToListAsync(cancellationToken);
    }

    private static void Apply(Contact contact, ContactType type, ContactStatus status, string fullName,
        string? company, string? jobTitle, string email, string phone, string countryCode,
        string? address, string? tags, string? notes, Guid? customerId, Guid? supplierId,
        Guid? branchId, Guid? ownerId, bool isPrimary)
    {
        contact.Type = type; contact.Status = status; contact.FullName = fullName.Trim();
        contact.CompanyName = Clean(company); contact.JobTitle = Clean(jobTitle);
        contact.Email = email.Trim(); contact.Phone = phone.Trim();
        contact.CountryCode = countryCode.Trim().ToUpperInvariant(); contact.Address = Clean(address);
        contact.Tags = Clean(tags); contact.Notes = Clean(notes); contact.CustomerId = customerId;
        contact.SupplierId = supplierId;
        contact.PreferredBranchId = branchId; contact.OwnerUserId = ownerId;
        contact.IsPrimary = isPrimary;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsCorporateType(ContactType type) =>
        type is ContactType.Director or ContactType.Shareholder;
    private static bool IsRetiredDirectoryType(ContactType type) =>
        type is ContactType.Client or ContactType.Prospect or ContactType.Shareholder or
            ContactType.ServiceProvider;
    private bool CanManageCorporateContacts() => User.IsInRole(nameof(UserRole.SuperAdmin)) ||
        User.IsInRole(nameof(UserRole.Administrator));
}

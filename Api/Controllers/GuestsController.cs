using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Route("api/guests")]
public sealed class GuestsController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IIntegrationGateway _integrations;
    private readonly IClientIdentityService _identity;
    public GuestsController(IApplicationDbContext context, IIntegrationGateway integrations,
        IClientIdentityService identity)
    {
        _context = context; _integrations = integrations; _identity = identity;
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("PublicWrite")]
    public async Task<ActionResult> Create(CreateGuestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest("An email address or phone number is required.");
        var guest = new Guest { FullName = request.FullName.Trim(), Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone.Trim(), CompanyName = request.CompanyName?.Trim(), CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            Source = request.Source.Trim(), ServiceInterest = request.ServiceInterest.Trim(), Notes = request.Notes?.Trim() };
        _context.Guests.Add(guest); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/guests/{guest.Id}", new { guest.Id, guest.Status, Message = "Guest enquiry received." });
    }

    [Authorize(Policy = "StaffOnly")]
    [HttpGet]
    public async Task<ActionResult> Search([FromQuery] GuestStatus? status, [FromQuery] string? search,
        [FromQuery] int take = 100, [FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Guests.AsNoTracking().AsQueryable(); if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(x => x.FullName.ToLower().Contains(term) || x.Email.ToLower().Contains(term) || x.Phone.Contains(term)); }
        var ordered = query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        if (paged) return Ok(await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken));
        return Ok(await ordered.Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken));
    }

    [Authorize(Policy = "StaffOnly")]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateGuestRequest request, CancellationToken cancellationToken)
    {
        var guest = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Guest not found.");
        if (guest.Status == GuestStatus.Converted) return Conflict("Converted guests are read-only.");
        guest.Status = request.Status; guest.Notes = request.Notes?.Trim(); guest.AssignedToUserId = request.AssignedToUserId;
        if (request.Status == GuestStatus.Contacted) guest.LastContactedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [Authorize(Policy = "SalesTeam")]
    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult> Convert(Guid id, ConvertGuestRequest request, CancellationToken cancellationToken)
    {
        var guest = await _context.Guests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Guest not found.");
        if (guest.Status == GuestStatus.Converted) return Conflict("Guest has already been converted.");
        if (string.IsNullOrWhiteSpace(guest.Email)) return BadRequest("An email address is required before client conversion.");
        var input = new ClientIdentityInput(guest.FullName, guest.Email, guest.Phone, null);
        var selectedCustomerId = await _identity.GetSelectedExistingClientAsync(input,
            request.DuplicateReviewId, cancellationToken);
        Customer? customer = null;
        if (selectedCustomerId.HasValue)
            customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == selectedCustomerId, cancellationToken)
                ?? throw new KeyNotFoundException("Selected client no longer exists.");
        else
        {
            var duplicate = await _identity.ValidateNewClientAsync(input, request.DuplicateReviewId,
                null, cancellationToken);
            if (duplicate is not null) return Conflict(duplicate);
        }
        if (customer is null)
        {
            var accountType = request.AccountType ?? (!string.IsNullOrWhiteSpace(guest.CompanyName)
                ? ClientAccountType.Business : ClientAccountType.Individual);
            customer = new Customer(Guid.NewGuid(), guest.FullName, guest.Email, 0, guest.CountryCode)
            {
                Phone = guest.Phone, CompanyName = guest.CompanyName, AccountType = accountType,
                IsBusiness = accountType != ClientAccountType.Individual
            };
            _context.Customers.Add(customer); _context.NotificationPreferences.Add(new NotificationPreference { CustomerId = customer.Id });
        }
        if (!await _context.ClientProfiles.AnyAsync(x => x.CustomerId == customer.Id, cancellationToken))
            _context.ClientProfiles.Add(new ClientProfile { CustomerId = customer.Id, Status = ClientStatus.Active,
                AccountManagerUserId = request.AccountManagerUserId ?? CurrentUserId, PreferredBranchId = request.PreferredBranchId,
                BillingCurrency = request.BillingCurrency.Trim().ToUpperInvariant(), PaymentTermsDays = request.PaymentTermsDays });
        guest.Status = GuestStatus.Converted; guest.ConvertedCustomerId = customer.Id; guest.ConvertedAt = DateTime.UtcNow;
        await _integrations.SendNotificationAsync(IntegrationKind.Email, customer.Email, "guest-converted-to-client",
            new { customer.FullName, customer.Id, NextStep = "Register on the customer portal using this email address." }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { CustomerId = customer.Id, guest.Id, guest.Status, NotificationQueued = true });
    }
}

using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/company")]
public sealed class CompanyController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public CompanyController(IApplicationDbContext context) => _context = context;

    [HttpGet("profile")]
    public async Task<ActionResult<CompanyProfile>> GetProfile(CancellationToken cancellationToken) =>
        Ok(await _context.CompanyProfiles.AsNoTracking().FirstAsync(cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateCompanyProfileRequest request, CancellationToken cancellationToken)
    {
        var company = await _context.CompanyProfiles.FirstAsync(cancellationToken);
        company.LegalName = request.LegalName.Trim(); company.TradingName = request.TradingName.Trim();
        company.RegistrationNumber = request.RegistrationNumber.Trim(); company.TaxNumber = request.TaxNumber.Trim();
        company.Website = request.Website.Trim(); company.Email = request.Email.Trim().ToLowerInvariant(); company.Phone = request.Phone.Trim();
        company.RegisteredAddress = request.RegisteredAddress.Trim(); company.BaseCurrency = request.BaseCurrency.Trim().ToUpperInvariant();
        company.TimeZone = request.TimeZone.Trim(); company.LogoStorageKey = request.LogoStorageKey?.Trim();
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("branches/controls")]
    public async Task<ActionResult> GetBranchControls(CancellationToken cancellationToken) => Ok(await (
        from branch in _context.Branches.AsNoTracking()
        join control in _context.BranchControls.AsNoTracking() on branch.Id equals control.BranchId into controls
        from control in controls.DefaultIfEmpty()
        orderby branch.Name select new { branch, control }).ToListAsync(cancellationToken));

    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Administrator)},{nameof(UserRole.OperationsManager)}")]
    [HttpPut("branches/{branchId:guid}/control")]
    public async Task<IActionResult> UpdateBranchControl(Guid branchId, UpdateBranchControlRequest request, CancellationToken cancellationToken)
    {
        if (request.CapacityAlertPercent is < 1 or > 100 || request.DailyCargoLimit < 0) return BadRequest("Branch control values are invalid.");
        if (!await _context.Branches.AnyAsync(x => x.Id == branchId, cancellationToken)) return NotFound();
        var control = await _context.BranchControls.FirstOrDefaultAsync(x => x.BranchId == branchId, cancellationToken);
        if (control is null) { control = new BranchControl { BranchId = branchId }; _context.BranchControls.Add(control); }
        control.IsBookingEnabled = request.IsBookingEnabled;
        control.IsReceivingEnabled = request.IsReceivingEnabled;
        control.IsDispatchEnabled = request.IsDispatchEnabled;
        control.CapacityAlertPercent = request.CapacityAlertPercent;
        control.DailyCargoLimit = request.DailyCargoLimit; control.EmergencyNotice = request.EmergencyNotice?.Trim();
        control.LastControlledByUserId = CurrentUserId; control.LastControlledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken); return NoContent();
    }
}

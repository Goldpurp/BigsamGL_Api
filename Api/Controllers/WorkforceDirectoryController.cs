using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/workforce/directory")]
public sealed class WorkforceDirectoryController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public WorkforceDirectoryController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? search,
        [FromQuery] string? branchCode, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var query = from profile in _context.StaffProfiles.AsNoTracking()
                    join user in _context.UserAccounts.AsNoTracking()
                        on profile.UserAccountId equals user.Id
                    where user.Status == AccountStatus.Active && profile.Status == StaffStatus.Active
                    select new
                    {
                        UserId = user.Id, user.FullName, user.Email, user.Role,
                        profile.EmployeeNumber, profile.JobTitle, profile.Department, profile.BranchCode
                    };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.FullName.ToLower().Contains(value) ||
                x.Email.ToLower().Contains(value) || x.EmployeeNumber.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var code = branchCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.BranchCode == code);
        }
        return Ok(await query.OrderBy(x => x.FullName).ThenBy(x => x.UserId)
            .ToPagedResponseAsync(page, pageSize, true, cancellationToken));
    }
}

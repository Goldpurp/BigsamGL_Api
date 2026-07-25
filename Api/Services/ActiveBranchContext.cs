using Api.Exceptions;
using Application.Common;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public sealed record ActiveBranchScope(Guid? BranchId, string? BranchKey, string? Code, string? Name,
    string? CountryCode, string? City, string? Currency, string? TimeZone, bool IsAllBranches);

public interface IActiveBranchContext
{
    Task<ActiveBranchScope> ResolveAsync(bool mutation, CancellationToken cancellationToken);
}

/// <summary>Enforces the selected branch before repositories build their object-level query.</summary>
public sealed class ActiveBranchContext : IActiveBranchContext
{
    private readonly IHttpContextAccessor _http;
    private readonly IApplicationDbContext _context;

    public ActiveBranchContext(IHttpContextAccessor http, IApplicationDbContext context)
    {
        _http = http;
        _context = context;
    }

    public async Task<ActiveBranchScope> ResolveAsync(bool mutation, CancellationToken cancellationToken)
    {
        var http = _http.HttpContext ?? throw new UnauthorizedAccessException("Request context is unavailable.");
        if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var userId))
            throw new UnauthorizedAccessException("Staff identity is missing.");
        var user = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId,
            cancellationToken) ?? throw new UnauthorizedAccessException("Staff account was not found.");
        if (user.Role == UserRole.Customer)
            throw new UnauthorizedAccessException("Customer accounts cannot use the staff dashboard.");

        var requested = http.Request.Headers["X-Active-Branch"].FirstOrDefault()?.Trim();
        if (string.Equals(requested, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (user.Role != UserRole.SuperAdmin)
                throw new ApiProblemException(StatusCodes.Status403Forbidden, "ALL_BRANCHES_FORBIDDEN",
                    "All branches is restricted", "Only Super Admin can select All branches.");
            if (mutation)
                throw new ApiProblemException(StatusCodes.Status422UnprocessableEntity, "BRANCH_REQUIRED",
                    "Select one branch", "A create or financial change requires Manchester, Lagos or Guangzhou.");
            return new ActiveBranchScope(null, "all", null, "All branches", null, null, null, null, true);
        }

        var memberships = await (from membership in _context.StaffBranchMemberships.AsNoTracking()
                                 join branch in _context.Branches.AsNoTracking()
                                     on membership.BranchId equals branch.Id
                                 where membership.UserAccountId == userId && branch.IsActive
                                 orderby membership.IsPrimary descending, branch.Code
                                 select new { membership.IsPrimary, Branch = branch })
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            var profileCode = await _context.StaffProfiles.AsNoTracking()
                .Where(x => x.UserAccountId == userId).Select(x => x.BranchCode)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(profileCode))
            {
                var fallback = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.IsActive && x.Code == profileCode, cancellationToken);
                if (fallback is not null) memberships.Add(new { IsPrimary = true, Branch = fallback });
            }
        }
        if (memberships.Count == 0 && user.Role == UserRole.SuperAdmin)
        {
            var activeBranches = await _context.Branches.AsNoTracking().Where(x => x.IsActive)
                .OrderBy(x => x.Code).ToListAsync(cancellationToken);
            memberships.AddRange(activeBranches.Select(x => new { IsPrimary = false, Branch = x }));
        }
        if (memberships.Count == 0)
            throw new ApiProblemException(StatusCodes.Status422UnprocessableEntity, "BRANCH_REQUIRED",
                "No branch membership", "Ask an Administrator to assign this staff account to a hub.");

        Branch? selected;
        if (string.IsNullOrWhiteSpace(requested))
            selected = memberships.First().Branch;
        else
        {
            selected = memberships.Select(x => x.Branch).FirstOrDefault(x => Matches(x, requested));
            if (selected is null && user.Role == UserRole.SuperAdmin)
                selected = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive &&
                    (x.Code.ToLower() == requested.ToLower() || Key(x) == requested.ToLower()), cancellationToken);
            if (selected is null)
                throw new ApiProblemException(StatusCodes.Status403Forbidden, "BRANCH_ACCESS_DENIED",
                    "Branch access denied", "The selected hub is not assigned to this staff account.");
        }

        var key = Key(selected);
        var city = key switch { "manchester" => "Manchester", "lagos" => "Lagos",
            "guangzhou" => "Guangzhou", _ => selected.Name };
        return new ActiveBranchScope(selected.Id, key, selected.Code, selected.Name,
            selected.CountryCode, city, CountryCurrency.ForCountry(selected.CountryCode), selected.TimeZone, false);
    }

    private static bool Matches(Branch branch, string requested) =>
        string.Equals(branch.Code, requested, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Key(branch), requested, StringComparison.OrdinalIgnoreCase);

    private static string Key(Branch branch) => branch.Code switch
    {
        "UK-MAN" => "manchester",
        "NG-LOS" => "lagos",
        "CN-GZ" => "guangzhou",
        _ => branch.Code.ToLowerInvariant()
    };
}

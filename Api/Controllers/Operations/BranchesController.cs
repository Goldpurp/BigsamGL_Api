using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Route("api/operations/branches")]
[Route("api/branches")]
public sealed class BranchesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    public BranchesController(IApplicationDbContext context) => _context = context;

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await _context.Branches.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<ActionResult<Branch>> Create(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var branch = new Branch { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(), TimeZone = request.TimeZone.Trim(), Address = request.Address.Trim() };
        _context.Branches.Add(branch); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/branches/{branch.Id}", branch);
    }
}

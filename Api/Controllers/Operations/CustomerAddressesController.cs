using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers.Operations;

[ApiController]
[Authorize(Roles = nameof(UserRole.Customer))]
[Route("api/operations/addresses")]
public sealed class CustomerAddressesController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public CustomerAddressesController(IApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<ActionResult<CustomerAddress>> Create(CreateAddressRequest request, CancellationToken cancellationToken)
    {
        if (request.IsDefault)
        {
            var defaults = await _context.CustomerAddresses.Where(x => x.CustomerId == CurrentCustomerId && x.Type == request.Type && x.IsDefault).ToListAsync(cancellationToken);
            foreach (var address in defaults) address.IsDefault = false;
        }
        var entity = new CustomerAddress { CustomerId = CurrentCustomerId, Type = request.Type,
            ContactName = request.ContactName.Trim(), Phone = request.Phone.Trim(), Line1 = request.Line1.Trim(),
            Line2 = request.Line2?.Trim(), City = request.City.Trim(), State = request.State?.Trim(),
            PostalCode = request.PostalCode.Trim(), CountryCode = request.CountryCode.Trim().ToUpperInvariant(), IsDefault = request.IsDefault };
        _context.CustomerAddresses.Add(entity); await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/operations/addresses/{entity.Id}", entity);
    }

    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await _context.CustomerAddresses.AsNoTracking().Where(x => x.CustomerId == CurrentCustomerId)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Type).ToListAsync(cancellationToken));
}

using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
    protected Guid CurrentUserId => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var value)
        ? value : throw new UnauthorizedAccessException("User identity is missing.");
    protected Guid CurrentCustomerId => Guid.TryParse(User.FindFirstValue("customer_id"), out var value)
        ? value : throw new UnauthorizedAccessException("Customer identity is missing.");
}

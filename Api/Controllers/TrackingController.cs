using Application.DTOs;
using Application.Features.CargoItems.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

public class TrackingController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("{trackingNumber}")]
    [EnableRateLimiting("PublicRead")]
    public async Task<ActionResult<PublicTrackingResponse>> GetPublicTracking(string trackingNumber)
    {
        var query = new GetPublicTrackingQuery(trackingNumber);
        var result = await Mediator.Send(query);
        return Ok(result);
    }
}

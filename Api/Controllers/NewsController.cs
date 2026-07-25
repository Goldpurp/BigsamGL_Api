using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[Route("api/news")]
[AllowAnonymous]
[EnableRateLimiting("PublicRead")]
public sealed class NewsController : ControllerBase
{
    private readonly IPublicNewsFeedService _newsFeed;

    public NewsController(IPublicNewsFeedService newsFeed) => _newsFeed = newsFeed;

    [HttpGet]
    [ProducesResponseType<PublicNewsFeed>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PublicNewsFeed>> Latest(
        [FromQuery] int limit = 36,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var feed = await _newsFeed.GetLatestAsync(cancellationToken);
            var safeLimit = Math.Clamp(limit, 8, 48);
            Response.Headers.CacheControl =
                "public, max-age=300, stale-while-revalidate=600";
            return Ok(feed with { Items = feed.Items.Take(safeLimit).ToArray() });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // The visitor left or refreshed while the feeds were loading.
            return new EmptyResult();
        }
        catch (PublicNewsFeedUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Fresh shipping news is unavailable.",
                detail: exception.Message);
        }
    }
}

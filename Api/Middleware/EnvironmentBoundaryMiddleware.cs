using Api.Configuration;

namespace Api.Middleware;

public sealed class EnvironmentBoundaryMiddleware
{
    public const string ClientHeader = "X-BiGSAM-App-Environment";
    public const string ApiHeader = "X-BiGSAM-API-Environment";
    private readonly RequestDelegate _next;

    public EnvironmentBoundaryMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IWebHostEnvironment environment)
    {
        var expected = environment.ClientName();
        context.Response.Headers[ApiHeader] = expected;
        context.Response.Headers.Append("Vary", ClientHeader);

        var supplied = context.Request.Headers[ClientHeader].FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(supplied) &&
            !supplied.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://api.bigsamgl.com/problems/environment-mismatch",
                title = "Application environment mismatch.",
                status = StatusCodes.Status409Conflict,
                detail = $"The {supplied} client cannot use the {expected} API.",
                correlationId = context.TraceIdentifier
            });
            return;
        }

        await _next(context);
    }
}

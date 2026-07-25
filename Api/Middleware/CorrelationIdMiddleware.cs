namespace Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = Guid.TryParse(supplied, out var parsed)
            ? parsed.ToString("D") : Guid.NewGuid().ToString("D");
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await _next(context);
    }
}


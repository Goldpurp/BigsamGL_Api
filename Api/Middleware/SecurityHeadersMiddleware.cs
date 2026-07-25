namespace Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IWebHostEnvironment environment)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            if (environment.IsProduction())
                headers.ContentSecurityPolicy =
                    "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            if (context.Request.Path.StartsWithSegments("/api/auth") ||
                context.Request.Headers.ContainsKey("Authorization"))
            {
                headers.CacheControl = "no-store";
                headers.Pragma = "no-cache";
            }
            return Task.CompletedTask;
        });
        await _next(context);
    }
}

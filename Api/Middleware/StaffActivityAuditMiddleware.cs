using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Api.Middleware;

public sealed class StaffActivityAuditMiddleware
{
    private static readonly string[] SensitiveQueryTerms =
        ["password", "token", "secret", "authorization", "key", "code"];
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaffActivityAuditMiddleware> _logger;

    public StaffActivityAuditMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory,
        ILogger<StaffActivityAuditMiddleware> logger)
    { _next = next; _scopeFactory = scopeFactory; _logger = logger; }

    public async Task InvokeAsync(HttpContext context)
    {
        var userIdValue = context.User.FindFirstValue("sub") ??
            context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleValue = context.User.FindFirstValue("role") ?? context.User.FindFirstValue(ClaimTypes.Role);
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(userIdValue, out var userId) ||
            !Enum.TryParse<UserRole>(roleValue, true, out var role) || role == UserRole.Customer)
        {
            await _next(context); return;
        }

        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var correlationId = GetCorrelationId(context);
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        var endpoint = context.GetEndpoint();
        var routeTemplate = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint?.DisplayName ?? string.Empty;
        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var path = $"{context.Request.PathBase}{context.Request.Path}";
        var query = SanitizeQuery(context.Request.Query);
        var method = context.Request.Method;
        var controller = action?.ControllerName ?? string.Empty;
        var actionName = action?.ActionName ?? string.Empty;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
        var deviceId = context.Request.Headers["X-Device-ID"].FirstOrDefault() ?? string.Empty;
        var sessionId = context.User.FindFirstValue("jti") ?? string.Empty;
        var location = Api.Services.StaffLocationHeaderParser.Read(context.Request.Headers);

        context.Response.OnCompleted(async () =>
        {
            try
            {
                stopwatch.Stop();
                await using var scope = _scopeFactory.CreateAsyncScope();
                var recorder = scope.ServiceProvider.GetRequiredService<IStaffActivityService>();
                await recorder.RecordAsync(new StaffActivityInput(userId, "HttpRequest", method,
                    routeTemplate, path, query, controller, actionName, context.Response.StatusCode,
                    OutcomeFor(context.Response.StatusCode), stopwatch.ElapsedMilliseconds, ip, userAgent,
                    deviceId, correlationId, sessionId, startedAt, location), CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not persist staff activity audit for correlation {CorrelationId}.",
                    correlationId);
            }
        });

        await _next(context);
    }

    private static StaffActivityOutcome OutcomeFor(int statusCode) => statusCode switch
    {
        >= 200 and < 400 => StaffActivityOutcome.Succeeded,
        401 => StaffActivityOutcome.Unauthorized,
        403 => StaffActivityOutcome.Forbidden,
        429 => StaffActivityOutcome.RateLimited,
        >= 500 => StaffActivityOutcome.ServerError,
        _ => StaffActivityOutcome.ClientError
    };

    private static string GetCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 100 &&
            supplied.All(x => char.IsLetterOrDigit(x) || x is '-' or '_' or '.' or ':')) return supplied;
        return string.IsNullOrWhiteSpace(context.TraceIdentifier) ? Guid.NewGuid().ToString("N") : context.TraceIdentifier;
    }

    private static string SanitizeQuery(IQueryCollection query)
    {
        if (query.Count == 0) return string.Empty;
        var result = new StringBuilder();
        foreach (var pair in query.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (result.Length > 0) result.Append('&');
            result.Append(Uri.EscapeDataString(pair.Key)).Append('=');
            var sensitive = SensitiveQueryTerms.Any(x => pair.Key.Contains(x, StringComparison.OrdinalIgnoreCase));
            result.Append(sensitive ? "[REDACTED]" : Uri.EscapeDataString(string.Join(',', pair.Value.ToArray())));
            if (result.Length >= 2000) break;
        }
        return result.Length <= 2000 ? result.ToString() : result.ToString(0, 2000);
    }
}

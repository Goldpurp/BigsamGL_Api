using Api.Configuration;
using Api.Middleware;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task InitializeBigsamAsync(this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        // EF migration tooling builds the host only to discover the model. It uses
        // ApplicationDbContextFactory and must never require runtime secrets.
        if (EF.IsDesignTime)
            return;

        StartupConfigurationValidator.Validate(app.Configuration, app.Environment);
        if (app.Configuration.GetValue("Database:SkipStartupInitialization", false))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        try
        {
            await initializer.InitializeAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DatabaseInitialization")
                .LogCritical(exception,
                    "Database initialization failed; the API will not accept traffic.");
            throw;
        }
    }

    public static WebApplication UseBigsamPipeline(this WebApplication app)
    {
        var runtime = app.Services.GetRequiredService<IOptions<RuntimeSettings>>().Value;
        var forwarded = app.Services
            .GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value;

        if (forwarded.Enabled) app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<EnvironmentBoundaryMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsProduction()) app.UseHsts();
        if (runtime.UseHttpsRedirection) app.UseHttpsRedirection();
        if (runtime.ResponseCompression) app.UseResponseCompression();
        app.UseRequestTimeouts();
        app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);
        app.UseAuthentication();
        app.UseMiddleware<StaffActivityAuditMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();

        if (runtime.EnableSwagger)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = string.Empty;
                options.SwaggerEndpoint(
                    "/swagger/v1/swagger.json",
                    "BiGSAM Global Logistics API v1");
                options.DocumentTitle =
                    $"BiGSAM API Console ({app.Environment.ClientName()})";
                options.DisplayRequestDuration();
                options.EnableDeepLinking();
                options.EnableFilter();
            });
        }

        app.MapGet("/health/live", () => Results.Ok(new
            {
                status = "Healthy",
                timestamp = DateTimeOffset.UtcNow,
                environment = app.Environment.ClientName()
            }))
            .AllowAnonymous()
            .ExcludeFromDescription();
        app.MapGet("/health/ready", async (ApplicationDbContext database,
                CancellationToken cancellationToken) =>
            await database.Database.CanConnectAsync(cancellationToken)
                ? Results.Ok(new
                {
                    status = "Ready",
                    database = "Connected",
                    checkedAt = DateTimeOffset.UtcNow,
                    environment = app.Environment.ClientName()
                })
                : Results.Json(new
                {
                    status = "NotReady",
                    database = "Unavailable",
                    checkedAt = DateTimeOffset.UtcNow,
                    environment = app.Environment.ClientName()
                }, statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous()
            .ExcludeFromDescription();
        app.MapControllers();
        return app;
    }
}

using System.Net;
using Npgsql;

namespace Api.Configuration;

public static class AppEnvironments
{
    public const string Local = "Local";
    public const string Testing = "Testing";
    public const string Production = "Production";

    public static bool IsLocal(this IHostEnvironment environment) =>
        environment.IsEnvironment(Local) || environment.IsDevelopment();

    public static bool IsTesting(this IHostEnvironment environment) =>
        environment.IsEnvironment(Testing);

    public static string ClientName(this IHostEnvironment environment) =>
        environment.IsProduction() ? "production" :
        environment.IsTesting() ? "testing" : "local";

    public static void EnsureSupported(IHostEnvironment environment)
    {
        if (!environment.IsLocal() && !environment.IsTesting() && !environment.IsProduction())
        {
            throw new InvalidOperationException(
                $"Unsupported environment '{environment.EnvironmentName}'. " +
                "Use Local, Development, Testing, or Production.");
        }
    }
}

public sealed class RuntimeSettings
{
    public bool EnableSwagger { get; init; }
    public bool UseHttpsRedirection { get; init; }
    public bool ResponseCompression { get; init; } = true;
    public int DefaultRequestTimeoutSeconds { get; init; } = 30;
    public int ReportRequestTimeoutSeconds { get; init; } = 60;
    public long MaximumRequestBodyBytes { get; init; } = 32 * 1024 * 1024;
    public int ShutdownTimeoutSeconds { get; init; } = 30;
}

public sealed class DatabaseRuntimeSettings
{
    public bool ApplyMigrationsOnStartup { get; init; }
    public bool InitializeOperationalData { get; init; }
    public bool TrustPrivateNetwork { get; init; }
    public int MinPoolSize { get; init; } = 2;
    public int MaxPoolSize { get; init; } = 50;
    public int ConnectionTimeoutSeconds { get; init; } = 5;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int ConnectionIdleLifetimeSeconds { get; init; } = 300;
}

public sealed class ForwardedHeadersSettings
{
    public bool Enabled { get; init; }
    public bool TrustPlatformProxy { get; init; }
    public int ForwardLimit { get; init; } = 1;
    public string[] KnownProxies { get; init; } = [];
}

public sealed class DataProtectionSettings
{
    public string KeysPath { get; init; } = string.Empty;
    public string CertificatePath { get; init; } = string.Empty;
    public string CertificateBase64 { get; init; } = string.Empty;
    public string CertificatePassword { get; init; } = string.Empty;
}

public static class StartupConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        AppEnvironments.EnsureSupported(environment);
        var errors = new List<string>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var database = configuration.GetSection("Database")
            .Get<DatabaseRuntimeSettings>() ?? new DatabaseRuntimeSettings();
        var runtime = configuration.GetSection("Runtime")
            .Get<RuntimeSettings>() ?? new RuntimeSettings();
        var forwarded = configuration.GetSection("ForwardedHeaders")
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
        var dataProtection = configuration.GetSection("DataProtection")
            .Get<DataProtectionSettings>() ?? new DataProtectionSettings();
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var publicSiteBaseUrl = configuration["PublicSite:BaseUrl"];

        ValidateDatabase(connectionString, database, environment, errors);

        var jwtSecret = configuration["Jwt:Secret"];
        if (IsPlaceholder(jwtSecret) ||
            System.Text.Encoding.UTF8.GetByteCount(jwtSecret ?? string.Empty) < 32)
        {
            errors.Add("Jwt:Secret must be a non-placeholder value containing at least 32 UTF-8 bytes.");
        }
        if (string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"]) ||
            string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]))
        {
            errors.Add("Jwt:Issuer and Jwt:Audience are required.");
        }

        if (origins.Length == 0)
            errors.Add("Cors:AllowedOrigins must contain at least one browser origin.");
        var defaultBranchCode = configuration["CustomerPortal:DefaultBranchCode"];
        if (string.IsNullOrWhiteSpace(defaultBranchCode) ||
            defaultBranchCode.Length > 30 ||
            defaultBranchCode.Any(x => !char.IsLetterOrDigit(x) && x != '-'))
            errors.Add("CustomerPortal:DefaultBranchCode must be a valid configured branch code.");
        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.AbsolutePath != "/")
            {
                errors.Add($"CORS origin '{origin}' must be an HTTP(S) origin without a path.");
                continue;
            }
            if (environment.IsProduction() &&
                (uri.Scheme != Uri.UriSchemeHttps || IPAddress.TryParse(uri.Host, out var ip) &&
                    IPAddress.IsLoopback(ip) ||
                 uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Production CORS origin '{origin}' must use HTTPS and cannot be loopback.");
            }
        }

        if (!Uri.TryCreate(publicSiteBaseUrl, UriKind.Absolute, out var publicSiteUri) ||
            (publicSiteUri.Scheme != Uri.UriSchemeHttp && publicSiteUri.Scheme != Uri.UriSchemeHttps) ||
            publicSiteUri.Query.Length > 0 || publicSiteUri.Fragment.Length > 0)
        {
            errors.Add("PublicSite:BaseUrl must be an absolute HTTP(S) URL without a query or fragment.");
        }
        else if (environment.IsProduction() &&
                 (publicSiteUri.Scheme != Uri.UriSchemeHttps ||
                  publicSiteUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                  IPAddress.TryParse(publicSiteUri.Host, out var publicSiteAddress) &&
                  IPAddress.IsLoopback(publicSiteAddress)))
        {
            errors.Add("Production PublicSite:BaseUrl must use HTTPS and cannot be loopback.");
        }

        if (runtime.DefaultRequestTimeoutSeconds is < 5 or > 120)
            errors.Add("Runtime:DefaultRequestTimeoutSeconds must be between 5 and 120.");
        if (runtime.ReportRequestTimeoutSeconds is < 15 or > 300)
            errors.Add("Runtime:ReportRequestTimeoutSeconds must be between 15 and 300.");
        if (runtime.MaximumRequestBodyBytes is < 1_048_576 or > 104_857_600)
            errors.Add("Runtime:MaximumRequestBodyBytes must be between 1 MB and 100 MB.");

        if (environment.IsProduction())
        {
            ValidateProduction(configuration, database, runtime, forwarded, dataProtection, errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "BiGSAM startup configuration is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(x => $" - {x}")));
        }
    }

    private static void ValidateDatabase(string? connectionString,
        DatabaseRuntimeSettings settings, IHostEnvironment environment, ICollection<string> errors)
    {
        if (IsPlaceholder(connectionString))
        {
            errors.Add("ConnectionStrings:DefaultConnection is required.");
            return;
        }

        try
        {
            var value = new NpgsqlConnectionStringBuilder(connectionString);
            if (environment.IsProduction())
            {
                if (IsLoopback(value.Host))
                    errors.Add("Production cannot use a loopback PostgreSQL host.");
                if (value.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer &&
                    !settings.TrustPrivateNetwork)
                {
                    errors.Add(
                        "Production PostgreSQL must use SSL Mode=Require, VerifyCA, or VerifyFull unless Database:TrustPrivateNetwork is explicitly enabled.");
                }
            }
        }
        catch (ArgumentException)
        {
            errors.Add("ConnectionStrings:DefaultConnection is not a valid PostgreSQL connection string.");
        }
    }

    private static void ValidateProduction(IConfiguration configuration,
        DatabaseRuntimeSettings database, RuntimeSettings runtime,
        ForwardedHeadersSettings forwarded, DataProtectionSettings dataProtection,
        ICollection<string> errors)
    {
        if (runtime.EnableSwagger)
            errors.Add("Runtime:EnableSwagger must be false in Production.");
        if (database.ApplyMigrationsOnStartup)
            errors.Add("Database:ApplyMigrationsOnStartup must be false in Production.");
        if (database.InitializeOperationalData)
            errors.Add("Database:InitializeOperationalData must be false in Production.");
        if (configuration.GetValue("Seed:ReferenceData", false))
            errors.Add("Seed:ReferenceData must be false in Production.");
        if (configuration.GetValue("Seed:LocalOperationalData", false))
            errors.Add("Seed:LocalOperationalData must be false in Production.");
        if (!string.Equals(configuration["Integrations:Mode"], "Live",
                StringComparison.OrdinalIgnoreCase))
            errors.Add("Integrations:Mode must be Live in Production.");
        if (!Uri.TryCreate(configuration["Integrations:BaseUrl"], UriKind.Absolute,
                out var integrationUri) || integrationUri.Scheme != Uri.UriSchemeHttps)
            errors.Add("Integrations:BaseUrl must be an absolute HTTPS URL in Production.");
        else if (!string.IsNullOrEmpty(integrationUri.UserInfo) ||
                 integrationUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 IPAddress.TryParse(integrationUri.Host, out var integrationAddress) &&
                 IPAddress.IsLoopback(integrationAddress))
            errors.Add("Production Integrations:BaseUrl cannot contain credentials or use a loopback host.");
        if (IsPlaceholder(configuration["Integrations:ApiKey"]))
            errors.Add("Integrations:ApiKey must be a non-placeholder production secret.");
        if (!string.Equals(configuration["MscTracking:Mode"], "Live",
                StringComparison.OrdinalIgnoreCase))
            errors.Add("MscTracking:Mode must be Live in Production.");
        var mscEventsUrl = configuration["MscTracking:EventsUrlTemplate"];
        if (!Uri.TryCreate(mscEventsUrl?.Replace("{reference}", "reference")
                .Replace("{containerNumber}", "reference")
                .Replace("{transportDocumentReference}", "reference")
                .Replace("{carrierBookingReference}", "reference"), UriKind.Absolute,
                out var mscEventsUri) || mscEventsUri.Scheme != Uri.UriSchemeHttps ||
            mscEventsUrl is null ||
            !mscEventsUrl.Contains("{reference}", StringComparison.OrdinalIgnoreCase) &&
            !mscEventsUrl.Contains("{containerNumber}", StringComparison.OrdinalIgnoreCase) &&
            !mscEventsUrl.Contains("{transportDocumentReference}", StringComparison.OrdinalIgnoreCase) &&
            !mscEventsUrl.Contains("{carrierBookingReference}", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("MscTracking:EventsUrlTemplate must be an HTTPS URL containing an approved reference placeholder.");
        }
        var hasMscApiToken = !IsPlaceholder(configuration["MscTracking:ApiToken"]);
        var hasMscOAuth = Uri.TryCreate(configuration["MscTracking:OAuthTokenUrl"],
                UriKind.Absolute, out var mscOAuthUri) &&
            mscOAuthUri.Scheme == Uri.UriSchemeHttps &&
            !IsPlaceholder(configuration["MscTracking:ClientId"]) &&
            !IsPlaceholder(configuration["MscTracking:ClientSecret"]);
        if (!hasMscApiToken && !hasMscOAuth)
            errors.Add("Configure an MSC API token or complete HTTPS OAuth credentials in Production.");

        var allowedHosts = configuration["AllowedHosts"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(allowedHosts) ||
            allowedHosts.Split(';', StringSplitOptions.TrimEntries)
                .Any(x => x == "*"))
        {
            errors.Add("Production AllowedHosts must contain explicit host names.");
        }

        if (!forwarded.Enabled)
            errors.Add("ForwardedHeaders:Enabled must be true in Production.");
        if (forwarded.ForwardLimit is < 1 or > 3)
            errors.Add("ForwardedHeaders:ForwardLimit must be between 1 and 3.");
        if (!forwarded.TrustPlatformProxy && forwarded.KnownProxies.Length == 0)
            errors.Add("Configure a trusted platform proxy or at least one exact known proxy.");
        foreach (var proxy in forwarded.KnownProxies)
            if (!IPAddress.TryParse(proxy, out _))
                errors.Add($"ForwardedHeaders known proxy '{proxy}' is not a valid IP address.");

        if (IsPlaceholder(dataProtection.KeysPath))
            errors.Add("Production DataProtection:KeysPath must use persistent storage.");
        if (IsPlaceholder(dataProtection.CertificatePath) &&
            IsPlaceholder(dataProtection.CertificateBase64))
        {
            errors.Add(
                "Production Data Protection keys must be protected with CertificatePath or CertificateBase64.");
        }
        if (IsPlaceholder(dataProtection.CertificatePassword))
            errors.Add("DataProtection:CertificatePassword is required in Production.");

        if (IsPlaceholder(configuration["CarrierWebhooks:SharedSecret"]))
            errors.Add("CarrierWebhooks:SharedSecret must be a non-placeholder production secret.");
    }

    private static bool IsLoopback(string? host) =>
        string.IsNullOrWhiteSpace(host) ||
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    internal static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim();
        return normalized.StartsWith('<') ||
               normalized.Contains("replace-", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("change-me", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("set_in_env", StringComparison.OrdinalIgnoreCase);
    }
}

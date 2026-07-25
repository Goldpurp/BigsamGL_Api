using System.IO.Compression;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Api.Configuration;
using Api.Services;
using Application;
using Application.Common.Interfaces;
using Domain.Enums;
using Infrastructure;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "BiGSAMFrontends";

    public static IServiceCollection AddBigsamApi(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var runtime = configuration.GetSection("Runtime").Get<RuntimeSettings>()
            ?? new RuntimeSettings();

        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IActiveBranchContext, ActiveBranchContext>();
        services.AddScoped<IManifestTrackingIngestionService, ManifestTrackingIngestionService>();
        services.AddMemoryCache();
        services.AddHttpClient("PublicNewsFeeds", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "BiGSAMGlobalLogistics/1.0 (+https://bigsamgl.com)");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression =
                DecompressionMethods.Brotli | DecompressionMethods.GZip |
                DecompressionMethods.Deflate,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            UseCookies = false
        });
        services.AddSingleton<IPublicNewsFeedService, PublicNewsFeedService>();
        services.AddHostedService<MscTrackingSyncWorker>();

        services.AddOptions<RuntimeSettings>().Bind(configuration.GetSection("Runtime"));
        services.AddOptions<DatabaseRuntimeSettings>().Bind(configuration.GetSection("Database"));
        services.AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection("ForwardedHeaders"));
        services.AddOptions<DataProtectionSettings>()
            .Bind(configuration.GetSection("DataProtection"));

        services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(
                Math.Clamp(runtime.ShutdownTimeoutSeconds, 5, 120)));
        services.Configure<FormOptions>(options =>
            options.MultipartBodyLengthLimit = Math.Clamp(runtime.MaximumRequestBodyBytes,
                1_048_576, 104_857_600));
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = Math.Clamp(runtime.MaximumRequestBodyBytes,
                1_048_576, 104_857_600);
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        });

        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.MaxDepth = 64;
        });
        services.AddProblemDetails();
        AddDataProtection(services, configuration, environment);
        services.AddHealthChecks();
        services.AddEndpointsApiExplorer();
        AddCompression(services);
        AddRequestTimeouts(services, runtime);
        AddAuthenticationAndAuthorization(services, configuration);
        AddCors(services, configuration);
        AddTrafficControls(services);
        AddForwardedHeaders(services, configuration);
        AddSwagger(services);

        return services;
    }

    private static void AddDataProtection(IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection("DataProtection")
            .Get<DataProtectionSettings>() ?? new DataProtectionSettings();
        var builder = services.AddDataProtection()
            .SetApplicationName("BiGSAM.Global.Logistics");

        if (!string.IsNullOrWhiteSpace(settings.KeysPath) &&
            !StartupConfigurationValidator.IsPlaceholder(settings.KeysPath))
        {
            var path = Path.GetFullPath(settings.KeysPath);
            Directory.CreateDirectory(path);
            builder.PersistKeysToFileSystem(new DirectoryInfo(path));
        }

        if (!string.IsNullOrWhiteSpace(settings.CertificateBase64) &&
            !StartupConfigurationValidator.IsPlaceholder(settings.CertificateBase64))
        {
            var certificate = new X509Certificate2(
                Convert.FromBase64String(settings.CertificateBase64),
                settings.CertificatePassword,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            builder.ProtectKeysWithCertificate(certificate);
        }
        else if (!string.IsNullOrWhiteSpace(settings.CertificatePath) &&
                 !StartupConfigurationValidator.IsPlaceholder(settings.CertificatePath))
        {
            var certificate = new X509Certificate2(
                settings.CertificatePath,
                settings.CertificatePassword,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            builder.ProtectKeysWithCertificate(certificate);
        }
        else if (environment.IsProduction())
        {
            // The startup validator produces the actionable configuration error.
        }
    }

    private static void AddCompression(IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/json", "application/problem+json", "text/csv"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);
    }

    private static void AddRequestTimeouts(IServiceCollection services, RuntimeSettings runtime)
    {
        services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(
                    Math.Clamp(runtime.DefaultRequestTimeoutSeconds, 5, 120))
            };
            options.AddPolicy("Reports", TimeSpan.FromSeconds(
                Math.Clamp(runtime.ReportRequestTimeoutSeconds, 15, 300)));
            options.AddPolicy("ProviderWebhook", TimeSpan.FromSeconds(15));
        });
    }

    private static void AddAuthenticationAndAuthorization(IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => Encoding.UTF8.GetByteCount(options.Secret) >= 32,
                "Jwt:Secret must contain at least 32 bytes.")
            .ValidateOnStart();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((options, configured) =>
            {
                var jwt = configured.Value;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        var staffRoles = Enum.GetNames<UserRole>()
            .Where(x => x != nameof(UserRole.Customer)).ToArray();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("StaffOnly", policy => policy.RequireRole(staffRoles));
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(
                nameof(UserRole.SuperAdmin), nameof(UserRole.Administrator)));
            options.AddPolicy("OperationsTeam", policy => policy.RequireRole(
                nameof(UserRole.SuperAdmin), nameof(UserRole.Administrator),
                nameof(UserRole.OperationsManager), nameof(UserRole.Staff)));
            options.AddPolicy("SalesTeam", policy => policy.RequireRole(
                nameof(UserRole.SuperAdmin), nameof(UserRole.Administrator),
                nameof(UserRole.SalesManager), nameof(UserRole.Staff)));
            options.AddPolicy("FinanceTeam", policy => policy.RequireRole(
                nameof(UserRole.SuperAdmin), nameof(UserRole.Administrator),
                nameof(UserRole.FinanceManager), nameof(UserRole.Staff)));
            options.AddPolicy("FinanceApprover", policy => policy.RequireRole(
                nameof(UserRole.SuperAdmin), nameof(UserRole.Administrator),
                nameof(UserRole.FinanceManager)));
            options.AddPolicy("InventoryTeam", policy => policy.RequireRole(
                nameof(UserRole.SuperAdmin), nameof(UserRole.Administrator),
                nameof(UserRole.OperationsManager), nameof(UserRole.FinanceManager),
                nameof(UserRole.Staff)));
        });
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins =
            configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders(
                    Middleware.CorrelationIdMiddleware.HeaderName,
                    Middleware.EnvironmentBoundaryMiddleware.ApiHeader)));
    }

    private static void AddTrafficControls(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Request capacity temporarily reached.",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Please wait briefly and retry the request."
                }, cancellationToken);
            };
            var totalConcurrency = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetConcurrencyLimiter("application",
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 200,
                        QueueLimit = 100,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
            var callerTraffic = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetTokenBucketLimiter(CallerKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 300,
                        TokensPerPeriod = 300,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 20,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));
            options.GlobalLimiter =
                PartitionedRateLimiter.CreateChained(totalConcurrency, callerTraffic);
            options.AddPolicy("Authentication", context => TokenBucket(
                $"auth:{context.Connection.RemoteIpAddress}", 60, TimeSpan.FromMinutes(1)));
            options.AddPolicy("PublicWrite", context => TokenBucket(
                $"public-write:{context.Connection.RemoteIpAddress}", 20,
                TimeSpan.FromMinutes(1)));
            options.AddPolicy("PublicRead", context => TokenBucket(
                $"public-read:{context.Connection.RemoteIpAddress}", 120,
                TimeSpan.FromMinutes(1), 10));
            options.AddPolicy("Search", context => TokenBucket(
                $"search:{CallerKey(context)}", 90, TimeSpan.FromMinutes(1), 5));
            options.AddPolicy("ProviderWebhook", context => TokenBucket(
                $"webhook:{context.Connection.RemoteIpAddress}", 300,
                TimeSpan.FromMinutes(1), 20));
            options.AddPolicy("CarrierSync", _ =>
                TokenBucket("carrier-sync", 4, TimeSpan.FromSeconds(1), 12));
            options.AddPolicy("Reports", context =>
                RateLimitPartition.GetConcurrencyLimiter($"report:{CallerKey(context)}",
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 3,
                        QueueLimit = 3,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });
    }

    private static RateLimitPartition<string> TokenBucket(string key, int tokens,
        TimeSpan period, int queue = 0) =>
        RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokens,
            TokensPerPeriod = tokens,
            ReplenishmentPeriod = period,
            QueueLimit = queue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });

    private static string CallerKey(HttpContext context)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(subject)
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"user:{subject}";
    }

    private static void AddForwardedHeaders(IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("ForwardedHeaders")
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = Math.Clamp(settings.ForwardLimit, 1, 3);
            options.RequireHeaderSymmetry = true;
            if (settings.TrustPlatformProxy)
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            }
            else
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var value in settings.KnownProxies)
                    if (IPAddress.TryParse(value, out var address))
                        options.KnownProxies.Add(address);
            }
        });
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "BiGSAM Global Logistics API",
                Version = "v1",
                Description =
                    "Branch-isolated logistics, sales, manifest tracking, finance, accounting, workforce and management services.",
                Contact = new OpenApiContact
                {
                    Name = "BiGSAM Global Logistics",
                    Email = "ops@bigsamgl.com",
                    Url = new Uri("https://bigsamgl.com")
                }
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = []
            });
            options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
            var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
        });
    }
}

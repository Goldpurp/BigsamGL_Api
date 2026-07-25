using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var runtimeConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = runtimeConfiguration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            var pooledConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = true,
                MinPoolSize = Math.Clamp(runtimeConfiguration.GetValue("Database:MinPoolSize", 2), 0, 20),
                MaxPoolSize = Math.Clamp(runtimeConfiguration.GetValue("Database:MaxPoolSize", 50), 10, 200),
                Timeout = Math.Clamp(runtimeConfiguration.GetValue("Database:ConnectionTimeoutSeconds", 5), 2, 30),
                CommandTimeout = Math.Clamp(runtimeConfiguration.GetValue("Database:CommandTimeoutSeconds", 30), 5, 120),
                ConnectionIdleLifetime = Math.Clamp(runtimeConfiguration.GetValue("Database:ConnectionIdleLifetimeSeconds", 300), 60, 1800),
                ConnectionPruningInterval = 10
            };
            options.UseNpgsql(pooledConnection.ConnectionString, builder =>
            {
                builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                builder.CommandTimeout(pooledConnection.CommandTimeout);
                builder.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });
        });

        // Application DbContext adapter binding
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Notification services registration
        services.AddScoped<IOmniChannelNotificationService, OmniChannelNotificationService>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<LocalOperationalDataSeeder>();
        services.AddScoped<PublishedServiceCatalogueSeeder>();
        var integrationMode = configuration["Integrations:Mode"];
        if (string.Equals(integrationMode, "Live", StringComparison.OrdinalIgnoreCase))
        {
            var timeoutSeconds = Math.Clamp(
                configuration.GetValue("Integrations:TimeoutSeconds", 15), 5, 60);
            services.AddHttpClient<IIntegrationGateway, LiveIntegrationGateway>(client =>
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds));
        }
        else
        {
            services.AddScoped<IIntegrationGateway, LocalIntegrationGateway>();
        }
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IClientCommunicationService, ClientCommunicationService>();
        services.AddScoped<IClientIdentityService, ClientIdentityService>();
        services.AddScoped<IInvoiceNumberGenerator, PostgresInvoiceNumberGenerator>();
        services.AddScoped<IShipmentInvoiceService, ShipmentInvoiceService>();
        services.AddScoped<ICompanyDocumentNumberGenerator, PostgresCompanyDocumentNumberGenerator>();
        services.AddScoped<IFinancialPostingService, FinancialPostingService>();
        services.AddScoped<IShipmentTrackingService, ShipmentTrackingService>();
        var exchangeRateTimeoutSeconds = Math.Clamp(
            configuration.GetValue("ExchangeRates:ProviderTimeoutSeconds", 5), 1, 15);
        services.AddHttpClient<ICurrencyExchangeService, CurrencyExchangeService>(client =>
            client.Timeout = TimeSpan.FromSeconds(exchangeRateTimeoutSeconds));
        services.AddOptions<MscTrackingOptions>()
            .Bind(configuration.GetSection(MscTrackingOptions.SectionName));
        services.AddHttpClient<IMscTrackingGateway, MscTrackingGateway>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<IContainerTrackingService, ContainerTrackingService>();
        services.AddScoped<IStaffActivityService, StaffActivityService>();
        services.AddScoped<ICargoAllocationService, CargoAllocationService>();
        services.AddScoped<ILegacyDataImportService, LegacyDataImportService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IEmailBroadcastService, EmailBroadcastService>();
        services.AddSingleton<IOfficeDocumentService, OfficeDocumentService>();
        services.AddHostedService<EmailBroadcastDispatcher>();

        return services;
    }
}

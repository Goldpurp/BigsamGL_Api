using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly LocalOperationalDataSeeder _localOperationalData;
    private readonly PublishedServiceCatalogueSeeder _publishedServiceCatalogue;

    public DatabaseInitializer(
        ApplicationDbContext context,
        IConfiguration configuration,
        LocalOperationalDataSeeder localOperationalData,
        PublishedServiceCatalogueSeeder publishedServiceCatalogue)
    {
        _context = context;
        _configuration = configuration;
        _localOperationalData = localOperationalData;
        _publishedServiceCatalogue = publishedServiceCatalogue;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var applyMigrations = _configuration.GetValue(
            "Database:ApplyMigrationsOnStartup", false);
        if (applyMigrations)
        {
            await _context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            if (!await _context.Database.CanConnectAsync(cancellationToken))
                throw new InvalidOperationException("The configured PostgreSQL database is not reachable.");
            var pending = (await _context.Database.GetPendingMigrationsAsync(cancellationToken))
                .ToArray();
            if (pending.Length > 0)
            {
                throw new InvalidOperationException(
                    "The database has pending migrations. Apply the reviewed migration bundle " +
                    $"before starting the API. Pending: {string.Join(", ", pending)}");
            }
        }

        if (!_configuration.GetValue("Database:InitializeOperationalData", false))
            return;

        await SeedChartOfAccountsAsync(cancellationToken);
        await SeedCompanyProfileAsync(cancellationToken);
        if (_configuration.GetValue("Seed:ReferenceData", false))
        {
            await SeedReferenceDataAsync(cancellationToken);
            await EnsureBackAndForthRoutesAsync(cancellationToken);
            await _publishedServiceCatalogue.SeedAsync(cancellationToken);
        }
        if (_configuration.GetValue("Seed:LocalOperationalData", false))
            await _localOperationalData.SeedAsync(cancellationToken);
        await CreateBootstrapAdministratorAsync(cancellationToken);
    }

    private async Task SeedCompanyProfileAsync(CancellationToken cancellationToken)
    {
        if (!await _context.CompanyProfiles.AnyAsync(cancellationToken))
        {
            _context.CompanyProfiles.Add(new CompanyProfile
            {
                LegalName = "BiGSAM Global Logistics Limited", TradingName = "BiGSAM Global Logistics",
                Website = "https://bigsamgl.com", Email = "ops@bigsamgl.com", Phone = "+44 161 223 2750",
                RegisteredAddress = "1 Kay St, Manchester M11 2DX", BaseCurrency = "GBP", TimeZone = "Europe/London"
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedChartOfAccountsAsync(CancellationToken cancellationToken)
    {
        var required = new[]
        {
            new LedgerAccount { Code = "1000", Name = "Cash", Type = AccountType.Asset, IsControlAccount = true },
            new LedgerAccount { Code = "1010", Name = "Bank", Type = AccountType.Asset, IsControlAccount = true },
            new LedgerAccount { Code = "1020", Name = "Card Payments Clearing", Type = AccountType.Asset, IsControlAccount = true },
            new LedgerAccount { Code = "1100", Name = "Client Invoice Balances", Type = AccountType.Asset, IsControlAccount = true },
            new LedgerAccount { Code = "1200", Name = "Input Tax Recoverable", Type = AccountType.Asset, IsControlAccount = true },
            new LedgerAccount { Code = "2000", Name = "Supplier Bills Payable", Type = AccountType.Liability, IsControlAccount = true },
            new LedgerAccount { Code = "2100", Name = "Customer Credit Available", Type = AccountType.Liability, IsControlAccount = true },
            new LedgerAccount { Code = "3000", Name = "Owner Capital", Type = AccountType.Equity, IsControlAccount = true },
            new LedgerAccount { Code = "4000", Name = "Shipping Service Revenue", Type = AccountType.Revenue },
            new LedgerAccount { Code = "4100", Name = "Other Income", Type = AccountType.Revenue },
            new LedgerAccount { Code = "5000", Name = "Shipping and Purchase Costs", Type = AccountType.Expense },
            new LedgerAccount { Code = "5200", Name = "Running Expenses", Type = AccountType.Expense }
        };
        var existing = await _context.LedgerAccounts.ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var account in required)
        {
            if (!existing.TryGetValue(account.Code, out var current))
                _context.LedgerAccounts.Add(account);
            else
            {
                current.Name = account.Name;
                current.Type = account.Type;
                current.IsControlAccount = account.IsControlAccount;
            }
        }
        if (_context.ChangeTracker.HasChanges()) await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedReferenceDataAsync(CancellationToken cancellationToken)
    {
        if (await _context.Branches.AnyAsync(cancellationToken)) return;
        var manchester = new Branch { Code = "UK-MAN", Name = "Manchester Warehouse", CountryCode = "GB", TimeZone = "Europe/London", Address = "1 Kay St, Manchester M11 2DX" };
        var lagos = new Branch { Code = "NG-LOS", Name = "Lagos Cargo Hub", CountryCode = "NG", TimeZone = "Africa/Lagos", Address = "Lagos, Nigeria" };
        var guangzhou = new Branch { Code = "CN-GZ", Name = "Guangzhou Sourcing Hub", CountryCode = "CN", TimeZone = "Asia/Shanghai", Address = "Guangzhou, China" };
        _context.Branches.AddRange(manchester, lagos, guangzhou);
        _context.BranchControls.AddRange(
            new BranchControl { BranchId = manchester.Id },
            new BranchControl { BranchId = lagos.Id },
            new BranchControl { BranchId = guangzhou.Id });

        var sea = new ServiceRoute { Code = "UK-NG-SEA", Name = "UK to Nigeria Sea Freight", OriginBranchId = manchester.Id, DestinationBranchId = lagos.Id, DestinationCountryCode = "NG", DestinationCity = "Lagos", EstimatedTimeline = "35–49 days", Mode = TransportMode.Sea, TransitDaysMin = 35, TransitDaysMax = 49, WeeklyDepartureDay = DayOfWeek.Tuesday, VolumetricDivisor = 1000000 };
        var air = new ServiceRoute { Code = "UK-NG-AIR", Name = "UK to Nigeria Air Freight", OriginBranchId = manchester.Id, DestinationBranchId = lagos.Id, DestinationCountryCode = "NG", DestinationCity = "Lagos", EstimatedTimeline = "5–10 days", Mode = TransportMode.Air, TransitDaysMin = 5, TransitDaysMax = 10, WeeklyDepartureDay = DayOfWeek.Thursday, VolumetricDivisor = 5000 };
        var china = new ServiceRoute { Code = "CN-NG-AIR", Name = "China to Nigeria Air Freight", OriginBranchId = guangzhou.Id, DestinationBranchId = lagos.Id, DestinationCountryCode = "NG", DestinationCity = "Lagos", EstimatedTimeline = "7–14 days", Mode = TransportMode.Air, TransitDaysMin = 7, TransitDaysMax = 14, WeeklyDepartureDay = DayOfWeek.Friday, VolumetricDivisor = 5000 };
        _context.ServiceRoutes.AddRange(sea, air, china);
        var now = DateTime.UtcNow.Date;
        _context.RateCards.AddRange(
            new RateCard { ServiceRouteId = sea.Id, CargoCategory = "General Cargo", MinimumWeightKg = 1, RatePerKg = 1.50m, MinimumCharge = 50, FuelSurchargeRate = 0.05m, CustomsRate = 0.02m, Currency = "GBP", EffectiveFrom = now },
            new RateCard { ServiceRouteId = air.Id, CargoCategory = "General Cargo", MinimumWeightKg = 5, RatePerKg = 4.50m, MinimumCharge = 45, FuelSurchargeRate = 0.08m, CustomsRate = 0.02m, Currency = "GBP", EffectiveFrom = now },
            new RateCard { ServiceRouteId = china.Id, CargoCategory = "General Cargo", MinimumWeightKg = 5, RatePerKg = 5.25m, MinimumCharge = 55, FuelSurchargeRate = 0.08m, CustomsRate = 0.02m, Currency = "GBP", EffectiveFrom = now });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBackAndForthRoutesAsync(CancellationToken cancellationToken)
    {
        var uk = await _context.Branches.FirstOrDefaultAsync(x => x.Code == "UK-MAN", cancellationToken);
        var nigeria = await _context.Branches.FirstOrDefaultAsync(x => x.Code == "NG-LOS", cancellationToken);
        if (uk is null || nigeria is null) return;
        var existing = await _context.ServiceRoutes.Where(x => x.Code == "NG-UK-AIR" ||
                x.Code == "NG-UK-SEA").Select(x => x.Code).ToListAsync(cancellationToken);
        if (!existing.Contains("NG-UK-AIR"))
            _context.ServiceRoutes.Add(new ServiceRoute
            {
                Code = "NG-UK-AIR", Name = "Nigeria to UK Air Freight",
                OriginBranchId = nigeria.Id, DestinationBranchId = uk.Id,
                DestinationCountryCode = "GB", DestinationCity = "Manchester",
                EstimatedTimeline = "5–10 days", Mode = TransportMode.Air,
                TransitDaysMin = 5, TransitDaysMax = 10, WeeklyDepartureDay = DayOfWeek.Thursday,
                VolumetricDivisor = 5000
            });
        if (!existing.Contains("NG-UK-SEA"))
            _context.ServiceRoutes.Add(new ServiceRoute
            {
                Code = "NG-UK-SEA", Name = "Nigeria to UK Sea Freight",
                OriginBranchId = nigeria.Id, DestinationBranchId = uk.Id,
                DestinationCountryCode = "GB", DestinationCity = "Manchester",
                EstimatedTimeline = "35–49 days", Mode = TransportMode.Sea,
                TransitDaysMin = 35, TransitDaysMax = 49, WeeklyDepartureDay = DayOfWeek.Tuesday,
                VolumetricDivisor = 1000000
            });
        if (_context.ChangeTracker.HasChanges()) await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateBootstrapAdministratorAsync(CancellationToken cancellationToken)
    {
        var email = _configuration["BootstrapAdmin:Email"]?.Trim();
        var password = _configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (password.Length < 12)
            throw new InvalidOperationException("Bootstrap administrator password must have at least 12 characters.");

        var normalizedEmail = email.ToUpperInvariant();
        var normalizedName = Application.Common.ClientIdentityNormalizer.Name(
            _configuration["BootstrapAdmin:FullName"] ?? "System Administrator");
        var employeeNumber = _configuration["BootstrapAdmin:EmployeeNumber"]?.Trim() ?? "BGL-ADMIN-001";
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            // A retry must begin with a clean unit of work so a transient failure cannot
            // leave inserted bootstrap entities tracked after their transaction rolled back.
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724519031)", cancellationToken);
            if (await _context.UserAccounts.AnyAsync(x => x.NormalizedEmail == normalizedEmail ||
                    x.NormalizedFullName == normalizedName, cancellationToken) ||
                await _context.StaffProfiles.AnyAsync(x => x.EmployeeNumber == employeeNumber, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var user = new UserAccount
            {
                FullName = _configuration["BootstrapAdmin:FullName"]?.Trim() ?? "System Administrator",
                NormalizedFullName = normalizedName,
                Email = email.ToLowerInvariant(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = PasswordService.Hash(password),
                Role = UserRole.SuperAdmin,
                Status = AccountStatus.Active,
                EmailVerifiedAt = DateTime.UtcNow
            };
            var profile = new StaffProfile
            {
                UserAccountId = user.Id,
                EmployeeNumber = employeeNumber,
                JobTitle = "System Administrator",
                Department = "Administration",
                BranchCode = _configuration["BootstrapAdmin:BranchCode"]?.Trim().ToUpperInvariant() ?? "GLOBAL",
                HireDate = DateTime.UtcNow.Date,
                Status = StaffStatus.Active
            };
            _context.UserAccounts.Add(user);
            _context.StaffProfiles.Add(profile);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}

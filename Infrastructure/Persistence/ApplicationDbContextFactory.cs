using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>Keeps EF tooling independent from API startup, hosted workers and live database seeding.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            "Host=127.0.0.1;Port=5432;Database=bigsamgl;Username=bigsamgl;Password=design-time-only";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection, builder => builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
        return new ApplicationDbContext(options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUserService
    {
        public string? UserId => "design-time";
        public string? IpAddress => null;
    }
}

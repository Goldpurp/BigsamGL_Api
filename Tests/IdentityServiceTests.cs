using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

public class IdentityServiceTests
{
    [Fact]
    public async Task RegistrationCreatesCustomerAccountAndRotatableSession()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var registered = await service.RegisterCustomerAsync(
            new RegisterCustomerRequest("Ada Customer", "ada@example.com", "StrongPassword!2026", "NG"),
            "127.0.0.1", default);
        var refreshed = await service.RefreshAsync(registered.RefreshToken, "127.0.0.1", default);

        Assert.Equal(UserRole.Customer, registered.User.Role);
        Assert.NotNull(registered.User.CustomerId);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
        Assert.Single(context.Customers);
        Assert.Equal(2, context.RefreshTokens.Count());
        Assert.Equal(1, context.RefreshTokens.Count(x => x.RevokedAt != null));
    }

    [Fact]
    public async Task LoginRejectsAnIncorrectPassword()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.RegisterCustomerAsync(
            new RegisterCustomerRequest("Ada Customer", "ada@example.com", "StrongPassword!2026", "NG"),
            null, default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest("ada@example.com", "not-the-password"), null, default));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options, new TestCurrentUser());
        context.Branches.Add(new Branch
        {
            Code = "UK-MAN",
            Name = "Manchester Warehouse",
            CountryCode = "GB",
            TimeZone = "Europe/London",
            Address = "Manchester"
        });
        context.SaveChanges();
        return context;
    }

    private static IdentityService CreateService(ApplicationDbContext context) =>
        new(context, Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            Secret = "a-test-secret-that-is-longer-than-thirty-two-bytes",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        }), new LocalIntegrationGateway(context));

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "test-user";
        public string? IpAddress => "127.0.0.1";
    }
}

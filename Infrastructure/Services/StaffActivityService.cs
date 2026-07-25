using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class StaffActivityService : IStaffActivityService
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    public StaffActivityService(IApplicationDbContext context, IConfiguration configuration)
    { _context = context; _configuration = configuration; }

    public async Task RecordAsync(StaffActivityInput input, CancellationToken cancellationToken)
    {
        var identity = await (from user in _context.UserAccounts.AsNoTracking()
            join staffProfile in _context.StaffProfiles.AsNoTracking() on user.Id equals staffProfile.UserAccountId into profiles
            from staffProfile in profiles.DefaultIfEmpty()
            where user.Id == input.UserAccountId && user.Role != UserRole.Customer
            select new { User = user, Profile = staffProfile }).FirstOrDefaultAsync(cancellationToken);
        if (identity is null) return;

        var now = DateTime.UtcNow;
        var location = input.Location;
        var freshness = StaffLocationFreshness.Unavailable;
        if (location is null)
        {
            var latest = await _context.StaffLocationPings.AsNoTracking().Where(x =>
                    x.UserAccountId == input.UserAccountId && x.ConsentConfirmed)
                .OrderByDescending(x => x.CapturedAt).FirstOrDefaultAsync(cancellationToken);
            var maximumAgeHours = Math.Clamp(_configuration.GetValue("Audit:MaximumLocationAgeHours", 24), 1, 168);
            if (latest is not null && latest.CapturedAt >= now.AddHours(-maximumAgeHours))
                location = new StaffLocationObservation(latest.Latitude, latest.Longitude,
                    latest.AccuracyMeters, latest.CapturedAt, StaffLocationSource.LastKnownBrowserGps,
                    latest.DeviceId, latest.Id);
        }
        if (location is not null)
        {
            var freshMinutes = Math.Clamp(_configuration.GetValue("Audit:LocationFreshMinutes", 30), 1, 240);
            freshness = location.CapturedAt >= now.AddMinutes(-freshMinutes)
                ? StaffLocationFreshness.Current : StaffLocationFreshness.Stale;
        }

        var profile = identity.Profile;
        _context.StaffActivityLogs.Add(new StaffActivityLog
        {
            UserAccountId = identity.User.Id, StaffProfileId = profile?.Id,
            StaffName = Limit(identity.User.FullName, 160), StaffEmail = Limit(identity.User.Email, 254),
            Role = identity.User.Role.ToString(), EmployeeNumber = Limit(profile?.EmployeeNumber, 50),
            BranchCode = Limit(profile?.BranchCode, 30), ActivityType = Limit(input.ActivityType, 50),
            HttpMethod = Limit(input.HttpMethod.ToUpperInvariant(), 12), RouteTemplate = Limit(input.RouteTemplate, 500),
            Path = Limit(input.Path, 1000), QueryString = Limit(input.QueryString, 2000),
            Controller = Limit(input.Controller, 120), ActionName = Limit(input.ActionName, 120),
            StatusCode = input.StatusCode, Outcome = input.Outcome,
            DurationMilliseconds = Math.Max(0, input.DurationMilliseconds), IpAddress = Limit(input.IpAddress, 64),
            UserAgent = Limit(input.UserAgent, 600), DeviceId = Limit(input.DeviceId, 120),
            CorrelationId = Limit(input.CorrelationId, 100), SessionId = Limit(input.SessionId, 100),
            LocationPingId = location?.LocationPingId, LocationSource = location?.Source ?? StaffLocationSource.None,
            LocationFreshness = freshness, Latitude = location?.Latitude, Longitude = location?.Longitude,
            LocationAccuracyMeters = location?.AccuracyMeters, LocationCapturedAt = location?.CapturedAt,
            OccurredAt = input.OccurredAt.ToUniversalTime()
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Limit(string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

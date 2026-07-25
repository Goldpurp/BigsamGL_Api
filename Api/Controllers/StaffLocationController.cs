using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("api/audit/location")]
public sealed class StaffLocationController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    public StaffLocationController(IApplicationDbContext context) => _context = context;

    [HttpPost]
    public async Task<ActionResult> Report(ReportStaffLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.ConsentConfirmed)
            return BadRequest("Location consent must be confirmed before a browser GPS position is stored.");
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return BadRequest("Latitude or longitude is outside its valid range.");
        if (request.AccuracyMeters is < 0 or > 100000)
            return BadRequest("Location accuracy must be between 0 and 100,000 metres.");
        if (request.DeviceId?.Length > 120)
            return BadRequest("Device ID cannot exceed 120 characters.");
        var capturedAt = request.CapturedAt.ToUniversalTime();
        if (capturedAt > DateTime.UtcNow.AddMinutes(5) || capturedAt < DateTime.UtcNow.AddHours(-24))
            return BadRequest("Location capture time must be within the last 24 hours and not in the future.");
        var user = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == CurrentUserId,
            cancellationToken) ?? throw new KeyNotFoundException("Staff user not found.");
        var profile = await _context.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(x =>
            x.UserAccountId == CurrentUserId, cancellationToken);
        var ping = new StaffLocationPing
        {
            UserAccountId = user.Id, StaffProfileId = profile?.Id, StaffName = user.FullName,
            EmployeeNumber = profile?.EmployeeNumber ?? string.Empty, BranchCode = profile?.BranchCode ?? string.Empty,
            Latitude = request.Latitude, Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters, CapturedAt = capturedAt, ReceivedAt = DateTime.UtcNow,
            Source = StaffLocationSource.BrowserGps, ConsentConfirmed = true,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            DeviceId = string.IsNullOrWhiteSpace(request.DeviceId)
                ? Request.Headers["X-Device-ID"].FirstOrDefault() ?? string.Empty : request.DeviceId.Trim()
        };
        _context.StaffLocationPings.Add(ping);
        await _context.SaveChangesAsync(cancellationToken);
        return Created($"/api/audit/locations/{ping.Id}", new
        {
            ping.Id, ping.Latitude, ping.Longitude, ping.AccuracyMeters, ping.CapturedAt,
            ping.ReceivedAt, ping.BranchCode, ping.Source
        });
    }
}

using Domain.Enums;

// Keep public contract namespace stable with the existing controllers.
namespace Api.Controllers;

public record ReportStaffLocationRequest(decimal Latitude, decimal Longitude, decimal AccuracyMeters,
    DateTime CapturedAt, string DeviceId, bool ConsentConfirmed);

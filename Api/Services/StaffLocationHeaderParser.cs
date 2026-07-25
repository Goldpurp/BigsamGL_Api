using System.Globalization;
using Application.Common.Interfaces;
using Domain.Enums;

namespace Api.Services;

public static class StaffLocationHeaderParser
{
    public static StaffLocationObservation? Read(IHeaderDictionary headers)
    {
        if (!bool.TryParse(headers["X-Staff-Location-Consent"].FirstOrDefault(), out var consent) || !consent)
            return null;
        if (!decimal.TryParse(headers["X-Staff-Latitude"].FirstOrDefault(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var latitude) || latitude is < -90 or > 90)
            return null;
        if (!decimal.TryParse(headers["X-Staff-Longitude"].FirstOrDefault(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var longitude) || longitude is < -180 or > 180)
            return null;
        var accuracy = decimal.TryParse(headers["X-Staff-Location-Accuracy"].FirstOrDefault(),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedAccuracy)
            ? Math.Clamp(parsedAccuracy, 0, 100000) : 0;
        var capturedAt = DateTime.TryParse(headers["X-Staff-Location-Captured-At"].FirstOrDefault(),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedCaptured)
            ? parsedCaptured.ToUniversalTime() : DateTime.UtcNow;
        if (capturedAt > DateTime.UtcNow.AddMinutes(5) || capturedAt < DateTime.UtcNow.AddHours(-24))
            return null;
        var deviceId = headers["X-Device-ID"].FirstOrDefault() ?? string.Empty;
        return new StaffLocationObservation(latitude, longitude, accuracy, capturedAt,
            StaffLocationSource.RequestHeaders, deviceId);
    }
}

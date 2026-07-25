using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class MscTrackingOptions
{
    public const string SectionName = "MscTracking";
    public string Mode { get; set; } = "Disabled";
    public string EventsUrlTemplate { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string OAuthTokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2.2.0";
    public string SubscriptionKeyHeader { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
    public bool AutomaticSyncEnabled { get; set; } = true;
    public int WorkerScanSeconds { get; set; } = 60;
    public int WorkerBatchSize { get; set; } = 20;
    public int FailureRetryMinutes { get; set; } = 15;
}

public sealed class MscTrackingGateway : IMscTrackingGateway
{
    private readonly HttpClient _httpClient;
    private readonly MscTrackingOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _accessTokenExpiresAt;

    public MscTrackingGateway(HttpClient httpClient, IOptions<MscTrackingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<MscContainerSnapshot> FetchContainerAsync(string containerNumber,
        string? bookingReference, string? billOfLadingNumber, CancellationToken cancellationToken) =>
        FetchAsync(containerNumber, CarrierTrackingReferenceType.ContainerNumber, cancellationToken);

    public async Task<MscContainerSnapshot> FetchAsync(string reference,
        CarrierTrackingReferenceType referenceType, CancellationToken cancellationToken)
    {
        var normalized = Normalize(reference, referenceType);
        if (string.Equals(_options.Mode, "Local", StringComparison.OrdinalIgnoreCase))
            return Local(normalized, referenceType);
        if (!string.Equals(_options.Mode, "Live", StringComparison.OrdinalIgnoreCase))
            throw new MscTrackingUnavailableException(
                "MSC tracking is disabled in this environment.");

        var url = BuildUrl(normalized, referenceType);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            await AccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(_options.ApiVersion))
            request.Headers.TryAddWithoutValidation("API-Version", _options.ApiVersion.Trim());
        if (!string.IsNullOrWhiteSpace(_options.SubscriptionKeyHeader) &&
            !string.IsNullOrWhiteSpace(_options.SubscriptionKey))
            request.Headers.TryAddWithoutValidation(_options.SubscriptionKeyHeader.Trim(),
                _options.SubscriptionKey.Trim());

        using var response = await _httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new MscTrackingReferenceNotFoundException(normalized, referenceType);
        if (!response.IsSuccessStatusCode)
            throw new MscTrackingUnavailableException(
                $"MSC Track & Trace returned HTTP {(int)response.StatusCode}. Try again shortly.");

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return ParseDcsa(normalized, referenceType, raw);
        }
        catch (JsonException exception)
        {
            throw new MscTrackingUnavailableException(
                "MSC returned a tracking response that could not be read as DCSA T&T v2.2.", exception);
        }
    }

    private string BuildUrl(string reference, CarrierTrackingReferenceType referenceType)
    {
        var template = _options.EventsUrlTemplate;
        if (string.IsNullOrWhiteSpace(template))
            throw new MscTrackingUnavailableException(
                "MSC live tracking is awaiting the EventsUrlTemplate issued during MSC onboarding.");

        var typePlaceholder = referenceType switch
        {
            CarrierTrackingReferenceType.ContainerNumber => "{containerNumber}",
            CarrierTrackingReferenceType.BillOfLading => "{transportDocumentReference}",
            CarrierTrackingReferenceType.BookingNumber => "{carrierBookingReference}",
            _ => throw new ArgumentOutOfRangeException(nameof(referenceType))
        };
        if (!template.Contains("{reference}", StringComparison.OrdinalIgnoreCase) &&
            !template.Contains(typePlaceholder, StringComparison.OrdinalIgnoreCase))
            throw new MscTrackingUnavailableException(
                $"MSC EventsUrlTemplate must contain {{reference}} or {typePlaceholder}.");

        var escaped = Uri.EscapeDataString(reference);
        return template.Replace("{reference}", escaped, StringComparison.OrdinalIgnoreCase)
            .Replace(typePlaceholder, escaped, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> AccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiToken)) return _options.ApiToken.Trim();
        if (string.IsNullOrWhiteSpace(_options.OAuthTokenUrl) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new MscTrackingUnavailableException(
                "MSC live tracking credentials are not configured. Add an API token or the OAuth credentials issued by MSC.");
        if (_accessToken is not null && _accessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
            return _accessToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _accessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
                return _accessToken;
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.OAuthTokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["scope"] = _options.Scope
                }.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
            };
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new MscTrackingUnavailableException(
                    "MSC authentication was not accepted. Verify the UAT or production credentials.");
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            _accessToken = Text(document.RootElement, "access_token");
            if (string.IsNullOrWhiteSpace(_accessToken))
                throw new MscTrackingUnavailableException("MSC authentication returned no access token.");
            var expiresIn = Number(document.RootElement, "expires_in") ?? 300;
            _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Clamp(expiresIn, 60, 86400));
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static MscContainerSnapshot ParseDcsa(string reference,
        CarrierTrackingReferenceType referenceType, string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var source = EventArray(document.RootElement);
        var mapped = source.Select((item, index) =>
        {
            var eventDomain = Text(item, "eventType") ?? EventDomain(item);
            var classifier = Text(item, "eventClassifierCode") ?? "ACT";
            var code = Text(item, "transportEventTypeCode") ??
                Text(item, "equipmentEventTypeCode") ??
                Text(item, "shipmentEventTypeCode") ??
                Text(item, "eventTypeCode") ?? "UPDATE";
            var occurred = Date(item, "eventDateTime") ??
                Date(item, "eventCreatedDateTime") ?? DateTime.UtcNow;
            var externalId = Text(item, "eventID") ?? Text(item, "eventId") ??
                StableId(reference, code, classifier, occurred, index);
            var location = Location(item);
            var estimated = Date(item, "estimatedArrivalDateTime");
            if (estimated is null && classifier.Equals("EST", StringComparison.OrdinalIgnoreCase) &&
                code.Equals("ARRI", StringComparison.OrdinalIgnoreCase))
                estimated = occurred;
            return new MscTrackingEvent(externalId, eventDomain, code, classifier,
                Describe(code, classifier, item), location, occurred, estimated, item.GetRawText());
        }).OrderBy(x => x.OccurredAt).ThenBy(x => x.ExternalEventId).ToList();

        var latestActual = mapped.LastOrDefault(x =>
            x.EventStatus.Equals("ACT", StringComparison.OrdinalIgnoreCase)) ?? mapped.LastOrDefault();
        var eta = mapped.Where(x => x.EstimatedArrivalAt.HasValue)
            .Select(x => x.EstimatedArrivalAt).LastOrDefault();
        return new MscContainerSnapshot(reference, Status(latestActual?.EventTypeCode),
            latestActual?.Location ?? string.Empty, eta, mapped, false);
    }

    private static MscContainerSnapshot Local(string reference,
        CarrierTrackingReferenceType referenceType)
    {
        var now = DateTime.UtcNow;
        var day = now.Date;
        var events = new List<MscTrackingEvent>
        {
            new($"msc-local-{reference}-GTIN-{day:yyyyMMdd}", "EQUIPMENT", "GTIN", "ACT",
                "Container gated in at the MSC origin terminal.", "Apapa, Lagos",
                day.AddHours(8), day.AddDays(21).AddHours(9),
                JsonSerializer.Serialize(new { local = true, reference, referenceType, eventType = "EQUIPMENT",
                    equipmentEventTypeCode = "GTIN", eventClassifierCode = "ACT" })),
            new($"msc-local-{reference}-LOAD-{day:yyyyMMdd}", "EQUIPMENT", "LOAD", "ACT",
                "Container loaded on the MSC vessel.", "Apapa, Lagos",
                day.AddHours(12), day.AddDays(21).AddHours(9),
                JsonSerializer.Serialize(new { local = true, reference, referenceType, eventType = "EQUIPMENT",
                    equipmentEventTypeCode = "LOAD", eventClassifierCode = "ACT" })),
            new($"msc-local-{reference}-DEPA-{day:yyyyMMdd}", "TRANSPORT", "DEPA", "ACT",
                "MSC vessel departed the origin port.", "Lagos, Nigeria",
                day.AddHours(14), day.AddDays(21).AddHours(9),
                JsonSerializer.Serialize(new { local = true, reference, referenceType, eventType = "TRANSPORT",
                    transportEventTypeCode = "DEPA", eventClassifierCode = "ACT" })),
            new($"msc-local-{reference}-ARRI-EST-{day:yyyyMMdd}", "TRANSPORT", "ARRI", "EST",
                "Estimated MSC vessel arrival.", "Felixstowe, United Kingdom",
                day.AddDays(21).AddHours(9), day.AddDays(21).AddHours(9),
                JsonSerializer.Serialize(new { local = true, reference, referenceType, eventType = "TRANSPORT",
                    transportEventTypeCode = "ARRI", eventClassifierCode = "EST" }))
        };
        return new MscContainerSnapshot(reference, "In transit", "Lagos, Nigeria",
            day.AddDays(21).AddHours(9), events, true);
    }

    private static List<JsonElement> EventArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray().ToList();
        if (Property(root, "events") is { ValueKind: JsonValueKind.Array } events)
            return events.EnumerateArray().ToList();
        return new List<JsonElement> { root };
    }

    private static string EventDomain(JsonElement item)
    {
        if (Property(item, "transportEventTypeCode") is not null) return "TRANSPORT";
        if (Property(item, "equipmentEventTypeCode") is not null) return "EQUIPMENT";
        if (Property(item, "shipmentEventTypeCode") is not null) return "SHIPMENT";
        return "UNKNOWN";
    }

    private static string Describe(string code, string classifier, JsonElement item)
    {
        var planned = classifier.ToUpperInvariant() switch
        {
            "EST" => "Estimated ",
            "PLN" => "Planned ",
            _ => string.Empty
        };
        var movement = code.ToUpperInvariant() switch
        {
            "GTIN" => "container gated in",
            "GTOT" => "container gated out",
            "LOAD" => "container loaded on vessel",
            "DISC" => "container discharged from vessel",
            "DEPA" => "vessel departure",
            "ARRI" => "vessel arrival",
            "STUF" => "container stuffed",
            "STRP" => "container stripped",
            "PICK" => "container picked up",
            "DROP" => "container dropped off",
            _ => $"shipment movement {code}"
        };
        var vessel = NestedText(item, "transportCall", "vessel", "vesselName") ??
            NestedText(item, "vessel", "vesselName");
        var voyage = NestedText(item, "transportCall", "carrierVoyageNumber") ??
            Text(item, "carrierVoyageNumber");
        var detail = string.Join(" · ", new[] { vessel, voyage }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var sentence = $"{planned}{movement}".Trim();
        return detail.Length == 0 ? $"MSC {sentence}." : $"MSC {sentence}: {detail}.";
    }

    private static string Location(JsonElement item)
    {
        var location = Property(item, "eventLocation") ?? Property(item, "location");
        if (location is { ValueKind: JsonValueKind.Object })
            return Text(location.Value, "locationName") ??
                Text(location.Value, "UNLocationCode") ??
                Text(location.Value, "facilityCode") ?? string.Empty;
        return location is { ValueKind: JsonValueKind.String } ? location.Value.GetString() ?? string.Empty :
            NestedText(item, "transportCall", "location", "locationName") ??
            NestedText(item, "transportCall", "location", "UNLocationCode") ?? string.Empty;
    }

    private static string Status(string? code) => code?.ToUpperInvariant() switch
    {
        "ARRI" or "DISC" => "Arrived",
        "GTOT" or "DELY" => "Released",
        null => "No events",
        _ => "In transit"
    };

    private static string Normalize(string reference, CarrierTrackingReferenceType referenceType)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("MSC tracking reference is required.", nameof(reference));
        var normalized = reference.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        if (normalized.Length is < 4 or > 100)
            throw new ArgumentException("MSC tracking reference must contain 4 to 100 characters.",
                nameof(reference));
        if (normalized.Any(x => !char.IsLetterOrDigit(x) && x is not '-' and not '/'))
            throw new ArgumentException("MSC tracking reference contains unsupported characters.",
                nameof(reference));
        if (referenceType == CarrierTrackingReferenceType.ContainerNumber &&
            (normalized.Length != 11 || normalized[..4].Any(x => !char.IsLetter(x)) ||
             normalized[4..].Any(x => !char.IsDigit(x))))
            throw new ArgumentException(
                "MSC container number must use the ISO 6346 format: four letters and seven digits.",
                nameof(reference));
        return normalized;
    }

    private static string StableId(string reference, string code, string classifier,
        DateTime occurred, int index) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{reference}:{code}:{classifier}:{occurred:O}:{index}")));
    private static JsonElement? Property(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in item.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }
    private static string? Text(JsonElement item, string name) =>
        Property(item, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    private static int? Number(JsonElement item, string name) =>
        Property(item, name) is { } value && value.TryGetInt32(out var parsed) ? parsed : null;
    private static DateTime? Date(JsonElement item, string name) =>
        Text(item, name) is { } value && DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.UtcDateTime : null;
    private static string? NestedText(JsonElement item, params string[] path)
    {
        var current = item;
        foreach (var segment in path)
        {
            var next = Property(current, segment);
            if (next is null) return null;
            current = next.Value;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}

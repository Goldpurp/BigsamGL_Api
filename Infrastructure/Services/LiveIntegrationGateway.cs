using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public sealed class LiveIntegrationGateway : IIntegrationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private readonly IApplicationDbContext _context;

    public LiveIntegrationGateway(HttpClient client, IApplicationDbContext context,
        IConfiguration configuration)
    {
        _client = client;
        _context = context;

        var baseUrl = configuration["Integrations:BaseUrl"]
            ?? throw new InvalidOperationException("Integrations:BaseUrl is required.");
        var apiKey = configuration["Integrations:ApiKey"]
            ?? throw new InvalidOperationException("Integrations:ApiKey is required.");
        _client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/", UriKind.Absolute);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task SendNotificationAsync(IntegrationKind channel, string recipient,
        string template, object payload, CancellationToken cancellationToken)
    {
        var request = new { channel, recipient, template, payload };
        await SendAsync("v1/notifications", request, cancellationToken);
        // Do not duplicate message bodies or attachment bytes in the generic integration log.
        // The owning communication entities remain the authoritative audit trail.
        Record(channel, "SendNotification", new
            {
                channel,
                recipient,
                template,
                payloadRecorded = false,
                note = "Delivery content is retained by the owning communication record."
            }, new { accepted = true });
    }

    public async Task<PaymentSession> CreatePaymentSessionAsync(string reference,
        decimal amount, string currency, CancellationToken cancellationToken)
    {
        var request = new { reference, amount, currency };
        var result = await SendAsync<PaymentSession>(
            "v1/payments/sessions", request, cancellationToken);
        RequireSecureUrl(result.CheckoutUrl, "payment checkout");
        Record(IntegrationKind.Payment, "CreateCheckout", request, result);
        return result;
    }

    public async Task<CarrierUpdate> GetCarrierUpdateAsync(string provider,
        string trackingNumber, CancellationToken cancellationToken)
    {
        var request = new { provider, trackingNumber };
        var result = await SendAsync<CarrierUpdate>(
            "v1/carrier-updates/query", request, cancellationToken);
        if (string.IsNullOrWhiteSpace(result.ExternalEventId) ||
            string.IsNullOrWhiteSpace(result.Location))
            throw new InvalidOperationException("The live carrier provider returned an incomplete update.");
        Record(IntegrationKind.CarrierTracking, "GetTrackingUpdate", request, result);
        return result;
    }

    public async Task<StoredDocument> StoreDocumentAsync(string fileName,
        string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        var request = new { fileName, contentType, sizeBytes };
        var result = await SendAsync<StoredDocument>(
            "v1/documents/metadata", request, cancellationToken);
        RequireSecureUrl(result.DownloadUrl, "document download");
        Record(IntegrationKind.DocumentStorage, "StoreDocumentMetadata", request, result);
        return result;
    }

    private async Task SendAsync(string path, object payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(path, payload);
        using var response = await _client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"The live integration provider rejected {path} with status {(int)response.StatusCode}.");
    }

    private async Task<T> SendAsync<T>(string path, object payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(path, payload);
        using var response = await _client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"The live integration provider rejected {path} with status {(int)response.StatusCode}.");
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The live integration provider returned an empty response for {path}.");
    }

    private static HttpRequestMessage CreateRequest(string path, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return request;
    }

    private static void RequireSecureUrl(string value, string label)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException(
                $"The live integration provider returned an invalid {label} URL.");
    }

    private void Record(IntegrationKind kind, string operation, object request,
        object response) =>
        _context.IntegrationMessages.Add(new IntegrationMessage
        {
            Kind = kind,
            Provider = "BiGSAM Live Provider",
            Operation = operation,
            CorrelationId = $"live-{Guid.NewGuid():N}",
            RequestJson = JsonSerializer.Serialize(request, JsonOptions),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            Status = IntegrationMessageStatus.Succeeded,
            ProcessedAt = DateTime.UtcNow
        });
}

using System.Net;
using System.Text;
using Application.Common.Interfaces;
using Domain.Enums;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

public sealed class MscTrackingGatewayTests
{
    [Fact]
    public async Task LiveGateway_NormalizesDcsaTrackAndTraceEvents()
    {
        const string response = """
        [
          {
            "eventID": "evt-gate-in",
            "eventType": "EQUIPMENT",
            "eventClassifierCode": "ACT",
            "equipmentEventTypeCode": "GTIN",
            "eventDateTime": "2026-07-23T08:30:00Z",
            "eventLocation": { "locationName": "Liverpool Port" }
          },
          {
            "eventID": "evt-arrival-estimate",
            "eventType": "TRANSPORT",
            "eventClassifierCode": "EST",
            "transportEventTypeCode": "ARRI",
            "eventDateTime": "2026-08-28T09:00:00Z",
            "eventLocation": { "UNLocationCode": "NGAPP" },
            "transportCall": {
              "carrierVoyageNumber": "AU126W",
              "vessel": { "vesselName": "MSC Aurora" }
            }
          }
        ]
        """;
        var handler = new StubHandler(response);
        var gateway = new MscTrackingGateway(new HttpClient(handler), Options.Create(
            new MscTrackingOptions
            {
                Mode = "Live",
                EventsUrlTemplate = "https://msc.test/events?equipmentReference={containerNumber}",
                ApiToken = "test-token",
                ApiVersion = "2.2.0"
            }));

        var result = await gateway.FetchAsync("MSCU1234567",
            CarrierTrackingReferenceType.ContainerNumber, default);

        Assert.False(result.IsMock);
        Assert.Equal(2, result.Events.Count);
        Assert.Equal("EQUIPMENT", result.Events[0].EventClassifier);
        Assert.Equal("ACT", result.Events[0].EventStatus);
        Assert.Equal("Liverpool Port", result.CurrentLocation);
        Assert.Equal(DateTime.Parse("2026-08-28T09:00:00Z").ToUniversalTime(),
            result.EstimatedArrivalAt);
        Assert.Contains("MSC Aurora", result.Events[1].Description);
        Assert.Contains("AU126W", result.Events[1].Description);
        Assert.Contains("equipmentReference=MSCU1234567", handler.RequestUri);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal("2.2.0", handler.ApiVersion);
    }

    [Fact]
    public async Task LiveGateway_FailsClosedWithoutMscOnboardingValues()
    {
        var gateway = new MscTrackingGateway(new HttpClient(new StubHandler("[]")),
            Options.Create(new MscTrackingOptions { Mode = "Live" }));

        await Assert.ThrowsAsync<MscTrackingUnavailableException>(() =>
            gateway.FetchAsync("MSCU1234567", CarrierTrackingReferenceType.ContainerNumber,
                default));
    }

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        public string RequestUri { get; private set; } = string.Empty;
        public string Authorization { get; private set; } = string.Empty;
        public string ApiVersion { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            Authorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            ApiVersion = request.Headers.TryGetValues("API-Version", out var values)
                ? values.Single() : string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}

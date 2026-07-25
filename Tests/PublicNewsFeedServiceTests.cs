using System.Net;
using System.Text;
using Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Tests;

public sealed class PublicNewsFeedServiceTests
{
    [Fact]
    public async Task Feed_IsSanitizedAllowListedAndCached()
    {
        var handler = new FeedHandler();
        var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var service = new PublicNewsFeedService(
            new ClientFactory(httpClient),
            cache,
            NullLogger<PublicNewsFeedService>.Instance);

        var first = await service.GetLatestAsync(CancellationToken.None);
        var second = await service.GetLatestAsync(CancellationToken.None);

        Assert.False(first.IsStale);
        Assert.Equal(4, first.SourceCount);
        Assert.Equal(4, first.Items.Count);
        Assert.Equal(4, handler.CallCount);
        Assert.Equal(first, second);
        Assert.All(first.Items, article =>
        {
            Assert.StartsWith("https://", article.Url);
            Assert.DoesNotContain('<', article.Summary);
            Assert.DoesNotContain("alert", article.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Null(article.ImageUrl);
            Assert.Null(article.Author);
        });
        Assert.Contains(first.Items, article => article.Category == "Air");
        Assert.Contains(first.Items, article => article.Category == "Ports");
        Assert.Contains(first.Items, article => article.Category == "Road");
        Assert.Contains(first.Items, article => article.Category == "Sea");
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var host = request.RequestUri!.Host;
            var title = host switch
            {
                "www.freightwaves.com" => "Road freight route opens",
                "theloadstar.com" => "Ocean vessel fleet expands",
                "www.aircargonews.net" => "Air cargo demand rises",
                _ => "Port delays ease this week"
            };
            var xml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <rss version="2.0"
                     xmlns:dc="http://purl.org/dc/elements/1.1/"
                     xmlns:media="http://search.yahoo.com/rss">
                  <channel>
                    <item>
                      <title>{title}</title>
                      <link>https://{host}/news/test-{CallCount}</link>
                      <dc:creator>SiteEditorial</dc:creator>
                      <pubDate>Fri, 24 Jul 2026 06:00:00 +0000</pubDate>
                      <category>Shipping news</category>
                      <description><![CDATA[<p>A <strong>clear</strong> shipping update.</p><script>alert('x')</script>]]></description>
                      <media:content url="https://tracker.example.test/image.jpg" />
                    </item>
                  </channel>
                </rss>
                """;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/rss+xml")
            };
            return Task.FromResult(response);
        }
    }
}

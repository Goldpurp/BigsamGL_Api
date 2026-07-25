using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Services;

public sealed record PublicNewsArticle(
    string Id,
    string Title,
    string Summary,
    string Url,
    string? ImageUrl,
    string Source,
    string? Author,
    string Category,
    DateTimeOffset PublishedAt);

public sealed record PublicNewsFeed(
    IReadOnlyList<PublicNewsArticle> Items,
    DateTimeOffset UpdatedAt,
    bool IsStale,
    int SourceCount);

public interface IPublicNewsFeedService
{
    Task<PublicNewsFeed> GetLatestAsync(CancellationToken cancellationToken);
}

public sealed class PublicNewsFeedUnavailableException : Exception
{
    public PublicNewsFeedUnavailableException()
        : base("Fresh shipping news is temporarily unavailable.")
    {
    }
}

public sealed class PublicNewsFeedService : IPublicNewsFeedService, IDisposable
{
    private const string FreshCacheKey = "public-news-feed:v1:fresh";
    private const string StaleCacheKey = "public-news-feed:v1:stale";
    private const int MaximumFeedBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan KeepStaleFor = TimeSpan.FromHours(12);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly XNamespace ContentNamespace =
        "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DcNamespace =
        "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace MediaNamespace = "http://search.yahoo.com/rss";

    private static readonly NewsFeedSource[] Sources =
    [
        new("FreightWaves", new Uri("https://www.freightwaves.com/feed"),
            ["freightwaves.com"]),
        new("The Loadstar", new Uri("https://theloadstar.com/feed/"),
            ["theloadstar.com"]),
        new("Air Cargo News", new Uri("https://www.aircargonews.net/feed/"),
            ["aircargonews.net"]),
        new("Hellenic Shipping News", new Uri("https://www.hellenicshippingnews.com/feed/"),
            ["hellenicshippingnews.com"])
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PublicNewsFeedService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public PublicNewsFeedService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<PublicNewsFeedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PublicNewsFeed> GetLatestAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(FreshCacheKey, out PublicNewsFeed? cached) && cached is not null)
            return cached;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(FreshCacheKey, out cached) && cached is not null)
                return cached;

            var fetched = await FetchAllAsync(cancellationToken);
            if (fetched.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                var fresh = new PublicNewsFeed(
                    fetched
                        .GroupBy(article => article.Id, StringComparer.Ordinal)
                        .Select(group => group.First())
                        .OrderByDescending(article => article.PublishedAt)
                        .Take(60)
                        .ToArray(),
                    now,
                    false,
                    fetched.Select(article => article.Source)
                        .Distinct(StringComparer.Ordinal)
                        .Count());

                _cache.Set(FreshCacheKey, fresh, FreshFor);
                _cache.Set(StaleCacheKey, fresh, KeepStaleFor);
                return fresh;
            }
        }
        finally
        {
            _refreshLock.Release();
        }

        if (_cache.TryGetValue(StaleCacheKey, out PublicNewsFeed? stale) && stale is not null)
            return stale with { IsStale = true };

        throw new PublicNewsFeedUnavailableException();
    }

    private async Task<List<PublicNewsArticle>> FetchAllAsync(
        CancellationToken cancellationToken)
    {
        var tasks = Sources.Select(source => FetchSourceAsync(source, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(items => items).ToList();
    }

    private async Task<IReadOnlyList<PublicNewsArticle>> FetchSourceAsync(
        NewsFeedSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.FeedUrl);
            request.Headers.Accept.ParseAdd(
                "application/rss+xml, application/xml;q=0.9, text/xml;q=0.8");
            using var response = await _httpClientFactory.CreateClient("PublicNewsFeeds")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaximumFeedBytes)
                throw new InvalidDataException("The RSS feed exceeded the size limit.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var safeCopy = new MemoryStream();
            var buffer = new byte[16 * 1024];
            var total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                total += read;
                if (total > MaximumFeedBytes)
                    throw new InvalidDataException("The RSS feed exceeded the size limit.");
                await safeCopy.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            safeCopy.Position = 0;
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumFeedBytes,
                IgnoreComments = true
            };
            using var reader = XmlReader.Create(safeCopy, settings);
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            return Parse(document, source).Take(18).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not refresh the {NewsSource} RSS feed.",
                source.Name);
            return [];
        }
    }

    private static IEnumerable<PublicNewsArticle> Parse(
        XDocument document,
        NewsFeedSource source)
    {
        var channel = document.Root?.Element("channel");
        if (channel is null) yield break;

        foreach (var item in channel.Elements("item"))
        {
            var title = PlainText(item.Element("title")?.Value, 180);
            var rawUrl = item.Element("link")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(title) ||
                !TryAllowedHttpsUrl(rawUrl, source.AllowedHosts, out var articleUrl))
                continue;

            var description = item.Element("description")?.Value ?? string.Empty;
            var content = item.Element(ContentNamespace + "encoded")?.Value ?? string.Empty;
            var summary = Summary(description, source.Name);
            var author = Author(item.Element(DcNamespace + "creator")?.Value);
            var publishedAt = ParseDate(item.Element("pubDate")?.Value);
            var categories = item.Elements("category").Select(value => value.Value);
            var category = Category(title, summary, categories, source.Name);
            var imageUrl = FindImage(item, description, content, source.AllowedHosts);

            yield return new PublicNewsArticle(
                StableId(articleUrl.AbsoluteUri),
                title,
                summary,
                articleUrl.AbsoluteUri,
                imageUrl,
                source.Name,
                author,
                category,
                publishedAt);
        }
    }

    private static string? FindImage(
        XElement item,
        string description,
        string content,
        IReadOnlyList<string> allowedHosts)
    {
        var media = item.Elements(MediaNamespace + "content")
            .Select(element => element.Attribute("url")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var enclosure = item.Elements("enclosure")
            .Where(element => element.Attribute("type")?.Value
                .StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            .Select(element => element.Attribute("url")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var html = string.Concat(description, " ", content);
        var htmlImage = Regex.Match(
            html,
            """<img\b[^>]*?\bsrc\s*=\s*["'](?<url>https://[^"']+)["']""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout).Groups["url"].Value;

        foreach (var candidate in new[] { media, enclosure, htmlImage })
            if (TryAllowedHttpsUrl(candidate, allowedHosts, out var imageUrl))
                return imageUrl.AbsoluteUri;

        return null;
    }

    private static string Summary(string html, string source)
    {
        var withoutUnsafeBlocks = Regex.Replace(
            html,
            """<(script|style)\b[^>]*>.*?</\1>""",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
            RegexTimeout);
        var withoutTags = Regex.Replace(
            withoutUnsafeBlocks,
            """<[^>]+>""",
            " ",
            RegexOptions.CultureInvariant,
            RegexTimeout);
        var decoded = WebUtility.HtmlDecode(withoutTags);
        decoded = Regex.Replace(
            decoded,
            """\s*The post .+? appeared first on .+?\.?\s*$""",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);
        var clean = PlainText(decoded, 420);
        return string.IsNullOrWhiteSpace(clean)
            ? $"Read the latest report from {source}."
            : clean;
    }

    private static string PlainText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var clean = WebUtility.HtmlDecode(value).Normalize(NormalizationForm.FormKC);
        clean = Regex.Replace(clean, """[\p{C}&&[^\r\n\t]]""", string.Empty,
            RegexOptions.CultureInvariant, RegexTimeout);
        clean = Regex.Replace(clean, """\s+""", " ",
            RegexOptions.CultureInvariant, RegexTimeout).Trim();
        if (clean.Length <= maximumLength) return clean;

        var shortened = clean[..maximumLength];
        var breakAt = Math.Max(
            shortened.LastIndexOf(". ", StringComparison.Ordinal),
            shortened.LastIndexOf(' '));
        if (breakAt >= maximumLength / 2) shortened = shortened[..breakAt];
        return string.Concat(shortened.TrimEnd(' ', '.', ',', ';', ':'), "…");
    }

    private static string? Author(string? value)
    {
        var author = PlainText(value, 80);
        if (string.IsNullOrWhiteSpace(author) ||
            author.Contains("editorial", StringComparison.OrdinalIgnoreCase) ||
            author.Contains("shippingnews", StringComparison.OrdinalIgnoreCase) ||
            author.EndsWith("...", StringComparison.Ordinal))
            return null;
        return author;
    }

    private static DateTimeOffset ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTimeOffset.UtcNow;

    private static string Category(
        string title,
        string summary,
        IEnumerable<string> categories,
        string source)
    {
        var text = string.Join(' ', categories.Prepend(source).Prepend(summary).Prepend(title))
            .ToLowerInvariant();
        if (ContainsAny(text, "airport", "air cargo", "airfreight", "air freight",
                "airline", "aviation", "freighter", "aircraft"))
            return "Air";
        if (ContainsAny(text, "port", "ports", "terminal", "harbour", "harbor",
                "dock", "teu"))
            return "Ports";
        if (ContainsAny(text, "ship", "vessel", "maritime", "ocean", "tanker",
                "seafarer", "dry bulk"))
            return "Sea";
        if (ContainsAny(text, "truck", "road", "driver", "last mile", "courier",
                "rail", "intermodal"))
            return "Road";
        if (ContainsAny(text, "customs", "tariff", "trade", "import", "export",
                "container", "supply chain"))
            return "Trade";
        return "Logistics";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => Regex.IsMatch(
            value,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term).Replace(@"\ ", @"\s+")}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout));

    private static bool TryAllowedHttpsUrl(
        string? value,
        IReadOnlyList<string> allowedHosts,
        out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) &&
            candidate.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(candidate.UserInfo) &&
            allowedHosts.Any(host =>
                candidate.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                candidate.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)))
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }

    private static string StableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..20].ToLowerInvariant();
    }

    public void Dispose() => _refreshLock.Dispose();

    private sealed record NewsFeedSource(
        string Name,
        Uri FeedUrl,
        IReadOnlyList<string> AllowedHosts);
}

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed class FashionMagazineService(
    IHttpClientFactory httpClientFactory,
    ILogger<FashionMagazineService> logger) : BackgroundService
{
    private static readonly (Uri Url, string Source)[] NewsFeeds =
    [
        (new Uri("https://www.elle.vn/thoi-trang/feed/"), "ELLE Việt Nam"),
        (new Uri("https://bazaarvietnam.vn/thoi-trang/feed/"), "Harper's Bazaar Việt Nam"),
        (new Uri("https://fashionista.com/.rss/full/"), "Fashionista")
    ];
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(1);
    private static readonly Regex HtmlTags = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ImageSource = new("<(?:img)\\b[^>]*?\\b(?:src|data-src)\\s*=\\s*[\\\"'](?<url>https://[^\\\"']+)[\\\"']", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private IReadOnlyList<FashionMagazineArticle> articles = Array.Empty<FashionMagazineArticle>();
    private DateTimeOffset? lastUpdatedAt;
    private DateTimeOffset? lastRefreshAttemptAt;

    public async Task<FashionMagazineViewModel> GetMagazineAsync(CancellationToken cancellationToken)
    {
        if (lastUpdatedAt is null || DateTimeOffset.UtcNow - lastUpdatedAt >= MaximumAge)
        {
            await RefreshAsync(cancellationToken);
        }

        return new FashionMagazineViewModel(articles, lastUpdatedAt, articles.Count > 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);
        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (lastRefreshAttemptAt is not null && DateTimeOffset.UtcNow - lastRefreshAttemptAt < RefreshInterval)
            {
                return;
            }

            lastRefreshAttemptAt = DateTimeOffset.UtcNow;
            var updated = new List<FashionMagazineArticle>();
            foreach (var feed in NewsFeeds)
            {
                updated.AddRange(await LoadFeedAsync(feed.Url, feed.Source, cancellationToken));
            }

            if (updated.Count == 0)
            {
                logger.LogWarning("Fashion magazine feeds returned no articles; keeping the last successful snapshot");
                return;
            }

            articles = updated
                .GroupBy(article => article.Url.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(article => article.PublishedAt)
                .Take(30)
                .ToArray();
            lastUpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Fashion magazine refreshed with {ArticleCount} articles", articles.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or XmlException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Unable to refresh the fashion magazine feed; keeping the last successful snapshot");
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task<IReadOnlyList<FashionMagazineArticle>> LoadFeedAsync(
        Uri feedUrl, string sourceName, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            request.Headers.UserAgent.ParseAdd("WStudioFashionMagazine/1.0");
            using var response = await httpClientFactory.CreateClient("fashion-news").SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 4_000_000
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            return document.Descendants("item")
                .Select(item => ParseArticle(item, sourceName))
                .Where(article => article is not null)
                .Select(article => article!)
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or XmlException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Unable to read fashion magazine feed from {FeedHost}", feedUrl.Host);
            return Array.Empty<FashionMagazineArticle>();
        }
    }

    private static FashionMagazineArticle? ParseArticle(XElement item, string sourceName)
    {
        var title = CleanText(item.Element("title")?.Value);
        var description = item.Element("description")?.Value;
        var summary = CleanText(description);
        var link = item.Element("link")?.Value?.Trim();
        var source = CleanText(item.Element("source")?.Value);
        var publishedText = item.Element("pubDate")?.Value;
        var imageUrl = ReadImageUrl(item, description);
        if (string.IsNullOrWhiteSpace(title)
            || !Uri.TryCreate(link, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            source = sourceName;
        }

        var publishedAt = DateTimeOffset.TryParse(
            publishedText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate)
            ? parsedDate
            : DateTimeOffset.UtcNow;
        return new FashionMagazineArticle(title, summary, source, url, imageUrl, publishedAt);
    }

    private static Uri? ReadImageUrl(XElement item, string? description)
    {
        XNamespace media = "http://search.yahoo.com/mrss/";
        var candidate = item.Element("enclosure")?.Attribute("url")?.Value
            ?? item.Element(media + "content")?.Attribute("url")?.Value
            ?? item.Element(media + "thumbnail")?.Attribute("url")?.Value;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            var encodedContent = item.Elements().FirstOrDefault(element => element.Name.LocalName == "encoded")?.Value;
            var match = ImageSource.Match(encodedContent ?? description ?? string.Empty);
            candidate = match.Success ? WebUtility.HtmlDecode(match.Groups["url"].Value) : null;
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var imageUrl)
            && imageUrl.Scheme == Uri.UriSchemeHttps
            ? imageUrl
            : null;
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        return Whitespace.Replace(HtmlTags.Replace(decoded, " "), " ").Trim();
    }
}

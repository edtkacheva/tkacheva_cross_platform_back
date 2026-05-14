using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Xml;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

public class RSSFeedService
{
    private readonly HttpClient _httpClient;

    public RSSFeedService(HttpClient httpClient)
    {
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static List<string> GetArticleCategories(SyndicationItem item)
    {
        return item.Categories
            .Select(c => c.Name)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<FeedValidationResult> ValidateAndReadFeedAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new FeedValidationResult
            {
                IsValid = false,
                ErrorMessage = "Некорректный URL."
            };
        }

        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return new FeedValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Источник недоступен. HTTP {(int)response.StatusCode}."
                };
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);

            if (feed == null)
            {
                return new FeedValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "По ссылке не найден корректный RSS канал."
                };
            }

            var articles = feed.Items.Select(item => new FeedArticleDto
            {
                Title = item.Title?.Text ?? "Без названия",
                Url = GetArticleUrl(item),
                Description = item.Summary?.Text,
                PublishedAt = item.PublishDate != DateTimeOffset.MinValue
                ? item.PublishDate.UtcDateTime
                : DateTime.UtcNow,
                Categories = GetArticleCategories(item)
            })
            .Where(a => !string.IsNullOrWhiteSpace(a.Url))
            .ToList();

            return new FeedValidationResult
            {
                IsValid = true,
                FeedTitle = feed.Title?.Text,
                FeedDescription = feed.Description?.Text,
                Articles = articles
            };
        }
        catch (XmlException)
        {
            return new FeedValidationResult
            {
                IsValid = false,
                ErrorMessage = "Содержимое по ссылке не является корректным XML RSS."
            };
        }
        catch (Exception ex)
        {
            return new FeedValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Ошибка при чтении RSS: {ex.Message}"
            };
        }
    }

    private static string GetArticleUrl(SyndicationItem item)
    {
        var articleLink = item.Links.FirstOrDefault(link =>
            link.Uri != null &&
            IsArticleLink(link));

        if (articleLink?.Uri != null)
            return articleLink.Uri.ToString();

        articleLink = item.Links.FirstOrDefault(link =>
            link.Uri != null &&
            !IsEnclosureLink(link) &&
            !IsImageLink(link));

        if (articleLink?.Uri != null)
            return articleLink.Uri.ToString();

        if (!string.IsNullOrWhiteSpace(item.Id) &&
            Uri.IsWellFormedUriString(item.Id, UriKind.Absolute) &&
            !IsImageUrl(item.Id))
        {
            return item.Id;
        }

        return "";
    }

    private static bool IsArticleLink(SyndicationLink link)
    {
        if (link.Uri == null)
            return false;

        if (IsImageLink(link))
            return false;

        if (IsEnclosureLink(link))
            return false;

        return string.Equals(
            link.RelationshipType,
            "alternate",
            StringComparison.OrdinalIgnoreCase
        )
        || string.IsNullOrWhiteSpace(link.RelationshipType);
    }

    private static bool IsEnclosureLink(SyndicationLink link)
    {
        return string.Equals(
            link.RelationshipType,
            "enclosure",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static bool IsImageLink(SyndicationLink link)
    {
        if (!string.IsNullOrWhiteSpace(link.MediaType) &&
            link.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return link.Uri != null && IsImageUrl(link.Uri.ToString());
    }

    private static bool IsImageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var path = uri.AbsolutePath.ToLowerInvariant();

        return path.EndsWith(".jpg")
            || path.EndsWith(".jpeg")
            || path.EndsWith(".png")
            || path.EndsWith(".gif")
            || path.EndsWith(".webp")
            || path.EndsWith(".svg");
    }
}
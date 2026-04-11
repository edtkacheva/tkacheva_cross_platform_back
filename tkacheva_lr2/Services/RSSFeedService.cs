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

            var finalUri = response.RequestMessage.RequestUri;

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
                Url = item.Links.FirstOrDefault()?.Uri?.ToString() ?? "",
                Description = item.Summary?.Text,
                PublishedAt = item.PublishDate != DateTimeOffset.MinValue
                    ? item.PublishDate.UtcDateTime
                    : DateTime.UtcNow
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
}
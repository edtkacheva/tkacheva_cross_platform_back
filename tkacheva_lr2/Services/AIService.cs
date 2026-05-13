using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;

namespace tkacheva_lr2.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            _httpClient.Timeout = TimeSpan.FromMinutes(5);

            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/124.0.0.0 Safari/537.36"
                );
            }
        }

        public async Task<List<KeywordDto>> ExtractKeywordsAccurateAsync(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("Некорректная ссылка на статью.");

            var totalWatch = Stopwatch.StartNew();

            var downloadWatch = Stopwatch.StartNew();
            var articleText = await DownloadAndExtractTextAsync(uri);
            downloadWatch.Stop();

            Console.WriteLine(
                $"[AI] Текст извлечён за {downloadWatch.Elapsed.TotalSeconds:F1} c. " +
                $"Длина: {articleText.Length}"
            );

            if (string.IsNullOrWhiteSpace(articleText) || articleText.Length < 80)
                throw new InvalidOperationException("Не удалось извлечь достаточно текста статьи.");

            var modelWatch = Stopwatch.StartNew();

            var keywords = await ExtractKeywordsFromTextAsync(
                articleText,
                modeName: "accurate",
                maxTextLength: 5000,
                numCtx: 4096,
                numPredict: 500
            );

            modelWatch.Stop();
            totalWatch.Stop();

            Console.WriteLine($"[AI] Модель ответила за {modelWatch.Elapsed.TotalSeconds:F1} c.");
            Console.WriteLine($"[AI] Всего: {totalWatch.Elapsed.TotalSeconds:F1} c.");

            return keywords;
        }

        private async Task<string> DownloadAndExtractTextAsync(Uri uri)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            request.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0.0.0 Safari/537.36"
            );

            request.Headers.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
            );

            request.Headers.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            request.Headers.Referrer = new Uri($"{uri.Scheme}://{uri.Host}/");

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Сайт вернул ошибку {(int)response.StatusCode} ({response.ReasonPhrase})."
                );
            }

            var html = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(html))
                return "";

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodesToRemove = doc.DocumentNode.SelectNodes(
                "//script|//style|//noscript|//nav|//header|//footer|//aside|//form|//svg"
            );

            if (nodesToRemove != null)
            {
                foreach (var node in nodesToRemove)
                    node.Remove();
            }

            var textParts = new List<string>();

            foreach (var metaText in ExtractMetaTexts(doc))
            {
                if (!string.IsNullOrWhiteSpace(metaText))
                    textParts.Add(metaText);
            }

            var paragraphNodes = doc.DocumentNode.SelectNodes("//article//p | //main//p | //p");

            if (paragraphNodes != null)
            {
                foreach (var p in paragraphNodes)
                {
                    var paragraphText = NormalizeText(p.InnerText);

                    if (paragraphText.Length >= 60)
                        textParts.Add(paragraphText);

                    var currentLength = string.Join(" ", textParts).Length;

                    if (currentLength >= 5000)
                        break;
                }
            }

            var result = NormalizeText(string.Join(" ", textParts.Distinct()));

            if (result.Length > 5000)
                result = result[..5000];

            return result;
        }

        private async Task<List<KeywordDto>> ExtractKeywordsFromTextAsync(
            string text,
            string modeName,
            int maxTextLength,
            int numCtx,
            int numPredict)
        {
            if (text.Length > maxTextLength)
                text = text[..maxTextLength];

            var prompt = $$"""
                Верни только JSON.

                Формат:
                {
                    "keywords": [
                    {
                        "text": "ключевое слово",
                        "weight": 0.95
                    }
                    ]
                }

                Задача: выдели ровно 5 ключевых слов для поиска по научной статье.

                Правила:
                - язык: русский;
                - 1-4 слова на ключевое слово;
                - включи главный объект статьи;
                - включи главный процесс или явление;
                - не используй общие слова: статья, новость, исследование, данные, автор, сайт, текст;
                - не смешивай кириллицу и латиницу внутри одного русского слова;
                - weight: число от 0 до 1;
                - JSON должен быть полностью закрыт.

                Текст:
                {{text}}
            """;

            var ollamaUrl = _configuration["AI:Ollama:ChatUrl"] ?? "http://localhost:11434/api/chat";
            var model = _configuration["AI:Ollama:Model"] ?? "gemma3:4b";

            var requestBody = new
            {
                model,
                stream = false,
                format = "json",
                keep_alive = "30m",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                options = new
                {
                    temperature = 0,
                    num_predict = numPredict,
                    num_ctx = numCtx
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(ollamaUrl, requestBody);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Ollama вернула ошибку {(int)response.StatusCode}: {responseJson}"
                );
            }

            using var doc = JsonDocument.Parse(responseJson);

            var content = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            Console.WriteLine($"[Ollama:{modeName}] Ответ модели:");
            Console.WriteLine(content);

            if (string.IsNullOrWhiteSpace(content))
                return new List<KeywordDto>();

            var cleanedJson = ExtractJsonObject(content);

            var result = JsonSerializer.Deserialize<KeywordResponseDto>(
                cleanedJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            return (result?.Keywords ?? new List<KeywordDto>())
                .Where(k => !string.IsNullOrWhiteSpace(k.Text))
                .Take(5)
                .ToList();
        }

        private static string ExtractJsonObject(string text)
        {
            text = text.Trim();

            text = text
                .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("```", "")
                .Trim();

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');

            if (start < 0 || end < 0 || end <= start)
                throw new InvalidOperationException("Модель вернула ответ не в формате JSON.");

            return text[start..(end + 1)];
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = HtmlEntity.DeEntitize(text);
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        private static List<string> ExtractMetaTexts(HtmlDocument doc)
        {
            var result = new List<string>();

            var xpaths = new[]
            {
                "//title",
                "//meta[@name='description']",
                "//meta[@property='og:title']",
                "//meta[@property='og:description']",
                "//meta[@name='twitter:title']",
                "//meta[@name='twitter:description']"
            };

            foreach (var xpath in xpaths)
            {
                var nodes = doc.DocumentNode.SelectNodes(xpath);

                if (nodes == null)
                    continue;

                foreach (var node in nodes)
                {
                    var value = node.Name.Equals("meta", StringComparison.OrdinalIgnoreCase)
                        ? node.GetAttributeValue("content", "")
                        : node.InnerText;

                    value = NormalizeText(value);

                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(value);
                }
            }

            return result;
        }
    }

    public class KeywordResponseDto
    {
        public List<KeywordDto> Keywords { get; set; } = new();
    }

    public class KeywordDto
    {
        public string Text { get; set; } = "";
        public double Weight { get; set; }
    }

}
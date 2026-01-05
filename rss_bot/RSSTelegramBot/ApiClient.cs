using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RSSTelegramBot;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public ApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BotConfig.ApiBaseUrl.TrimEnd('/') + "/")
        };
    }

    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_token))
            return;

        var body = new
        {
            userName = BotConfig.ApiUserName,
            password = BotConfig.ApiPassword
        };

        using var resp = await _httpClient.PostAsJsonAsync("api/auth/login", body, ct);
        resp.EnsureSuccessStatusCode();

        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        if (data == null || string.IsNullOrWhiteSpace(data.Token))
            throw new InvalidOperationException("Login response does not contain token.");

        _token = data.Token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    private async Task<T?> GetJsonWithAuthRetry<T>(string relativeUrl, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        using var resp = await _httpClient.GetAsync(relativeUrl, ct);

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            _token = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;

            await EnsureAuthAsync(ct);

            using var resp2 = await _httpClient.GetAsync(relativeUrl, ct);
            resp2.EnsureSuccessStatusCode();
            return await resp2.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    public async Task<List<RssChannel>> GetChannelsAsync(CancellationToken ct)
    {
        return await GetJsonWithAuthRetry<List<RssChannel>>("api/rss", ct)
               ?? new List<RssChannel>();
    }

    public async Task<List<Article>> GetArticlesAsync(CancellationToken ct)
    {
        return await GetJsonWithAuthRetry<List<Article>>("api/articles", ct)
               ?? new List<Article>();
    }

    public async Task<List<Article>> SearchArticlesAsync(string text, CancellationToken ct)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return new List<Article>();

        return await GetJsonWithAuthRetry<List<Article>>(
                   $"api/articles/search/description/{Uri.EscapeDataString(text)}",
                   ct
               ) ?? new List<Article>();
    }

    private class LoginResponse
    {
        public string Token { get; set; } = "";
    }
}

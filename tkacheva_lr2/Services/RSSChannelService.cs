using Microsoft.EntityFrameworkCore;
using System.Linq;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class RSSChannelService
    {
        private readonly ApplicationDbContext _context;
        private readonly RSSFeedService _rssFeedService;
        private readonly ArticleKeywordService _articleKeywordService;
        private readonly IArticleAiQueue _articleAiQueue;

        public RSSChannelService(
            ApplicationDbContext context,
            RSSFeedService rssFeedService,
            ArticleKeywordService articleKeywordService,
            IArticleAiQueue articleAiQueue)
        {
            _context = context;
            _rssFeedService = rssFeedService;
            _articleKeywordService = articleKeywordService;
            _articleAiQueue = articleAiQueue;
        }

        public async Task<List<RSSChannel>> GetAllChannelsAsync()
        {
            return await _context.RSSChannels
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new RSSChannel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Url = c.Url,
                    Description = c.Description
                })
                .ToListAsync();
        }

        public async Task<RSSChannel?> GetChannelByNameAsync(string name)
        {
            return await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        }

        public async Task<RSSChannel?> GetChannelByUrlAsync(string url)
        {
            var normalizedUrl = NormalizeUrlString(url);

            var channels = await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
                .ToListAsync();

            return channels.FirstOrDefault(c => NormalizeUrlString(c.Url) == normalizedUrl);
        }
        public async Task<RSSChannel> CreateChannelWithArticlesAsync(
            string channelName,
            string rssUrl,
            string creatorUsername)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Название канала не может быть пустым.");

            if (string.IsNullOrWhiteSpace(rssUrl))
                throw new ArgumentException("RSS URL не может быть пустым.");

            channelName = channelName.Trim();
            rssUrl = rssUrl.Trim();

            var normalizedNewUrl = NormalizeUrlString(rssUrl);

            var channelNameExists = await _context.RSSChannels
                .AnyAsync(c => c.Name.ToLower() == channelName.ToLower());

            if (channelNameExists)
                throw new InvalidOperationException("Канал с таким названием уже существует.");

            var existingChannels = await _context.RSSChannels
                .AsNoTracking()
                .ToListAsync();

            var channelUrlExists = existingChannels.Any(c =>
                NormalizeUrlString(c.Url) == normalizedNewUrl);

            if (channelUrlExists)
                throw new InvalidOperationException("Этот RSS-источник уже добавлен.");

            var feedResult = await _rssFeedService.ValidateAndReadFeedAsync(rssUrl);

            if (!feedResult.IsValid)
                throw new ArgumentException(feedResult.ErrorMessage ?? "Некорректный RSS-канал.");

            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == creatorUsername.ToLower());

            if (user == null)
                throw new InvalidOperationException("Пользователь не найден.");

            var channel = new RSSChannel
            {
                Name = channelName,
                Url = rssUrl,
                Description = feedResult.FeedDescription
            };

            _context.RSSChannels.Add(channel);
            await _context.SaveChangesAsync();

            var createdArticles = new List<Article>();

            foreach (var item in feedResult.Articles)
            {
                if (string.IsNullOrWhiteSpace(item.Url))
                    continue;

                var normalizedArticleUrl = item.Url.Trim().ToLower();

                var existingArticle = await _context.Articles
                    .FirstOrDefaultAsync(a => a.Url.ToLower() == normalizedArticleUrl);

                if (existingArticle != null)
                    continue;

                var article = new Article
                {
                    Title = item.Title,
                    Url = item.Url,
                    Description = item.Description,
                    PublishedAt = item.PublishedAt,
                    RSSChannelId = channel.Id
                };

                _context.Articles.Add(article);
                createdArticles.Add(article);
            }

            await _context.SaveChangesAsync();

            if (!user.IsAdmin())
            {
                var now = DateTime.UtcNow;

                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT OR IGNORE INTO UserChannelSubscriptions
                        (SubscribedChannelsId, SubscribersId)
                    VALUES
                        ({channel.Id}, {user.Id});
                ");

                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT OR IGNORE INTO UserArticleStates
                        (AppUserId, ArticleId, IsRead, AddedAt, ReadAt, IsFavorite)
                    SELECT
                        {user.Id},
                        a.Id,
                        0,
                        {now},
                        NULL,
                        0
                    FROM Articles AS a
                    WHERE a.RSSChannelId = {channel.Id};
                ");
            }

            await _context.SaveChangesAsync();

            foreach (var article in createdArticles)
            {
                await _articleKeywordService.EnsureBaseKeywordsAsync(article);
                await _articleAiQueue.EnqueueAsync(article.Id);
            }

            return channel;
        }

        public async Task<int> RefreshChannelsForUserAsync(string username)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                throw new InvalidOperationException("Пользователь не найден.");

            var addedArticlesCount = 0;

            foreach (var channel in user.SubscribedChannels)
            {
                addedArticlesCount += await RefreshSingleChannelForUserAsync(channel, user.Id);
            }

            return addedArticlesCount;
        }

        public async Task<int> RefreshChannelsForUserAsync(int userId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new InvalidOperationException("Пользователь не найден.");

            var addedArticlesCount = 0;

            foreach (var channel in user.SubscribedChannels)
            {
                addedArticlesCount += await RefreshSingleChannelForUserAsync(channel, user.Id);
            }

            return addedArticlesCount;
        }

        public async Task<int> RefreshAllChannelsAsync()
        {
            var channels = await _context.RSSChannels
                .AsNoTracking()
                .ToListAsync();

            var addedArticlesCount = 0;

            foreach (var channel in channels)
            {
                addedArticlesCount += await RefreshSingleChannelAsync(channel);
            }

            return addedArticlesCount;
        }

        private async Task<int> RefreshSingleChannelForUserAsync(RSSChannel channel, int userId)
        {
            var feedResult = await _rssFeedService.ValidateAndReadFeedAsync(channel.Url);

            if (!feedResult.IsValid)
            {
                Console.WriteLine($"RSS-канал '{channel.Name}' не обновлён: {feedResult.ErrorMessage}");
                return 0;
            }

            var addedArticlesCount = 0;

            foreach (var item in feedResult.Articles)
            {
                var article = await _context.Articles
                    .FirstOrDefaultAsync(a => a.Url.ToLower() == item.Url.ToLower());

                if (article == null)
                {
                    article = new Article
                    {
                        Title = item.Title,
                        Url = item.Url,
                        Description = item.Description,
                        PublishedAt = item.PublishedAt,
                        RSSChannelId = channel.Id
                    };

                    _context.Articles.Add(article);
                    await _context.SaveChangesAsync();

                    addedArticlesCount++;

                    await _articleKeywordService.EnsureBaseKeywordsAsync(article);
                    await _articleAiQueue.EnqueueAsync(article.Id);
                }

                var stateExists = await _context.UserArticleStates
                    .AnyAsync(s => s.AppUserId == userId && s.ArticleId == article.Id);

                if (!stateExists)
                {
                    _context.UserArticleStates.Add(new UserArticleState
                    {
                        AppUserId = userId,
                        ArticleId = article.Id,
                        IsRead = false,
                        AddedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            return addedArticlesCount;
        }

        private async Task<int> RefreshSingleChannelAsync(RSSChannel channel)
        {
            var feedResult = await _rssFeedService.ValidateAndReadFeedAsync(channel.Url);

            if (!feedResult.IsValid)
            {
                Console.WriteLine($"RSS-канал '{channel.Name}' не обновлён: {feedResult.ErrorMessage}");
                return 0;
            }

            var addedArticlesCount = 0;

            foreach (var item in feedResult.Articles)
            {
                var articleExists = await _context.Articles
                    .AnyAsync(a => a.Url.ToLower() == item.Url.ToLower());

                if (articleExists)
                    continue;

                var article = new Article
                {
                    Title = item.Title,
                    Url = item.Url,
                    Description = item.Description,
                    PublishedAt = item.PublishedAt,
                    RSSChannelId = channel.Id
                };

                _context.Articles.Add(article);
                addedArticlesCount++;
            }

            await _context.SaveChangesAsync();

            return addedArticlesCount;
        }

        public async Task<RSSChannel?> UpdateChannelAsync(int id, string name, string url)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Id == id);

            if (channel == null)
                return null;

            name = (name ?? "").Trim();
            url = (url ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название канала не может быть пустым.");

            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("RSS URL не может быть пустым.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new ArgumentException("Некорректный RSS URL.");
            }

            var nameExists = await _context.RSSChannels
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Name.ToLower() == name.ToLower());

            if (nameExists)
                throw new InvalidOperationException("Канал с таким названием уже существует.");

            var normalizedNewUrl = NormalizeUrlString(url);

            var otherChannels = await _context.RSSChannels
                .AsNoTracking()
                .Where(c => c.Id != id)
                .ToListAsync();

            var urlExists = otherChannels.Any(c =>
                NormalizeUrlString(c.Url) == normalizedNewUrl);

            if (urlExists)
                throw new InvalidOperationException("Канал с таким RSS URL уже существует.");

            var oldNormalizedUrl = NormalizeUrlString(channel.Url);
            var urlChanged = oldNormalizedUrl != normalizedNewUrl;

            if (urlChanged)
            {
                var feedResult = await _rssFeedService.ValidateAndReadFeedAsync(url);

                if (!feedResult.IsValid)
                    throw new ArgumentException(feedResult.ErrorMessage ?? "По ссылке не найден корректный RSS-канал.");

                channel.Description = feedResult.FeedDescription;
            }

            channel.Name = name;
            channel.Url = url;

            await _context.SaveChangesAsync();

            return channel;
        }

        public async Task<bool> DeleteChannelAsync(int channelId)
        {
            var channelExists = await _context.RSSChannels
                .AnyAsync(c => c.Id == channelId);

            if (!channelExists)
                return false;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DELETE FROM UserChannelSubscriptions
                WHERE SubscribedChannelsId = {channelId};
            ");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DELETE FROM UserArticleStates
                WHERE ArticleId IN (
                    SELECT Id
                    FROM Articles
                    WHERE RSSChannelId = {channelId}
                );
            ");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DELETE FROM Articles
                WHERE RSSChannelId = {channelId};
            ");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                DELETE FROM RSSChannels
                WHERE Id = {channelId};
            ");

            await transaction.CommitAsync();

            return true;
        }

        private async Task<bool> ChannelExistsAsync(string name)
        {
            return await _context.RSSChannels.AnyAsync(c =>
                c.Name.ToLower() == name.ToLower());
        }
        private static string NormalizeUrlString(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            url = url.Trim().ToLowerInvariant();
            return url.TrimEnd('/');
        }
    }
}
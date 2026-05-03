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

        public RSSChannelService(ApplicationDbContext context, RSSFeedService rssFeedService)
        {
            _context = context;
            _rssFeedService = rssFeedService;
        }

        public async Task<List<RSSChannel>> GetAllChannelsAsync()
        {
            return await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
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
        public async Task<RSSChannel> CreateChannelWithArticlesAsync(string channelName, string rssUrl, string creatorUsername)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Название канала не может быть пустым.");

            if (string.IsNullOrWhiteSpace(rssUrl))
                throw new ArgumentException("RSS URL не может быть пустым.");

            var normalizedNewUrl = NormalizeUrlString(rssUrl);

            if (await _context.RSSChannels.AnyAsync(c => c.Name.ToLower() == channelName.ToLower()))
                throw new InvalidOperationException("Канал с таким названием уже существует.");

            var existingChannels = await _context.RSSChannels.ToListAsync();
            if (existingChannels.Any(c => NormalizeUrlString(c.Url) == normalizedNewUrl))
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

            foreach (var item in feedResult.Articles)
            {
                var existingArticle = await _context.Articles
                    .FirstOrDefaultAsync(a => a.Url.ToLower() == item.Url.ToLower());

                if (existingArticle == null)
                {
                    var article = new Article
                    {
                        Title = item.Title,
                        Url = item.Url,
                        Description = item.Description,
                        PublishedAt = item.PublishedAt,
                        RSSChannelId = channel.Id
                    };
                    _context.Articles.Add(article);
                }
            }
            await _context.SaveChangesAsync();

            // Подписываем создателя на канал
            if (!user.SubscribedChannels.Any(c => c.Id == channel.Id))
            {
                user.SubscribedChannels.Add(channel);
            }

            // Создаём записи UserArticleState для создателя
            var articlesInChannel = await _context.Articles
                .Where(a => a.RSSChannelId == channel.Id)
                .ToListAsync();

            foreach (var article in articlesInChannel)
            {
                var exists = await _context.UserArticleStates
                    .AnyAsync(s => s.AppUserId == user.Id && s.ArticleId == article.Id);

                if (!exists)
                {
                    _context.UserArticleStates.Add(new UserArticleState
                    {
                        AppUserId = user.Id,
                        ArticleId = article.Id,
                        IsRead = false,
                        AddedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
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

        public async Task<RSSChannel?> UpdateChannelAsync(string name, RSSChannel updated)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

            if (channel == null) return null;

            if (!string.IsNullOrWhiteSpace(updated.Name) &&
                !channel.Name.Equals(updated.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await ChannelExistsAsync(updated.Name))
                    throw new InvalidOperationException("New channel name already exists.");

                channel.Name = updated.Name;
            }

            channel.Url = updated.Url;
            channel.Description = updated.Description;

            await _context.SaveChangesAsync();
            return channel;
        }

        public async Task<bool> DeleteChannelAsync(int channelId)
        {
            var channel = await _context.RSSChannels
                .Include(c => c.Subscribers)
                .Include(c => c.Articles)
                .FirstOrDefaultAsync(c => c.Id == channelId);

            if (channel == null)
                return false;

            // Удаляем связи с подписчиками
            channel.Subscribers.Clear();

            // Удаляем все UserArticleState для статей этого канала
            var articleIds = channel.Articles.Select(a => a.Id).ToList();
            var statesToDelete = await _context.UserArticleStates
                .Where(s => articleIds.Contains(s.ArticleId))
                .ToListAsync();
            _context.UserArticleStates.RemoveRange(statesToDelete);

            // Удаляем статьи
            _context.Articles.RemoveRange(channel.Articles);

            // Удаляем канал
            _context.RSSChannels.Remove(channel);
            await _context.SaveChangesAsync();

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
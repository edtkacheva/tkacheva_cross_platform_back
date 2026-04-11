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

        public async Task<RSSChannel> CreateChannelWithArticlesAsync(string channelName, string rssUrl, string username)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                throw new ArgumentException("Название канала не может быть пустым.");

            if (string.IsNullOrWhiteSpace(rssUrl))
                throw new ArgumentException("RSS URL не может быть пустым.");

            if (await _context.RSSChannels.AnyAsync(c => c.Name.ToLower() == channelName.ToLower()))
                throw new InvalidOperationException("Канал с таким названием уже существует.");

            if (await _context.RSSChannels.AnyAsync(c => c.Url.ToLower() == rssUrl.ToLower()))
                throw new InvalidOperationException("Этот RSS-источник уже добавлен.");

            var feedResult = await _rssFeedService.ValidateAndReadFeedAsync(rssUrl);

            if (!feedResult.IsValid)
                throw new ArgumentException(feedResult.ErrorMessage ?? "Некорректный RSS-канал.");

            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

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
            
            if (user.SubscribedChannels == null)
            {
                user.SubscribedChannels = new List<RSSChannel>();
            }
            if (!user.SubscribedChannels.Any(c => c.Id == channel.Id))
            {
                user.SubscribedChannels.Add(channel);
            }
            var addedUserArticleStatesInCurrentBatch = new HashSet<(int AppUserId, int ArticleId)>();

            foreach (var item in feedResult.Articles)
            {
                var articleExists = await _context.Articles.AnyAsync(a =>
                    a.Url.ToLower() == item.Url.ToLower());

                Article article;

                if (!articleExists)
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
                }
                else
                {
                    article = await _context.Articles.FirstAsync(a => a.Url.ToLower() == item.Url.ToLower());
                }

                var userArticleStateKey = (user.Id, article.Id);

                var stateExistsInDb = await _context.UserArticleStates.AnyAsync(x =>
                    x.AppUserId == user.Id && x.ArticleId == article.Id);

                if (!stateExistsInDb && !addedUserArticleStatesInCurrentBatch.Contains(userArticleStateKey))
                {
                    _context.UserArticleStates.Add(new UserArticleState
                    {
                        AppUserId = user.Id,
                        ArticleId = article.Id,
                        IsRead = false,
                        AddedAt = DateTime.UtcNow
                    });
                    addedUserArticleStatesInCurrentBatch.Add(userArticleStateKey);
                }
            }

            await _context.SaveChangesAsync();
            return channel;
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

        public async Task<bool> DeleteChannelAsync(string name)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

            if (channel == null) return false;

            _context.RSSChannels.Remove(channel);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> ChannelExistsAsync(string name)
        {
            return await _context.RSSChannels.AnyAsync(c =>
                c.Name.ToLower() == name.ToLower());
        }

    }
}
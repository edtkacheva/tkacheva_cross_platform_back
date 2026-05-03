using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;
        private readonly RSSFeedService _rssFeedService;

        public UserService(ApplicationDbContext context, RSSFeedService rssFeedService)
        {
            _context = context;
            _rssFeedService = rssFeedService;
        }

        public async Task<List<AppUser>> GetAllUsersAsync()
        {
            return await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .ToListAsync();
        }

        public async Task<AppUser?> GetUserByNameAsync(string username)
        {
            return await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());
        }

        public async Task<AppUser> CreateUserAsync(AppUser user)
        {
            if (!user.IsPasswordStrong())
                throw new ArgumentException("Password is too weak (min 6 chars).");

            if (await UserExistsAsync(user.UserName))
                throw new InvalidOperationException("UserName already exists.");

            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<AppUser?> UpdateUserAsync(string username, AppUser data)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(data.UserName))
            {
                var exists = await _context.AppUsers.AnyAsync(u =>
                    u.UserName.ToLower() == data.UserName.ToLower() && u.Id != user.Id);

                if (exists)
                    throw new InvalidOperationException("New username already taken.");

                user.UserName = data.UserName;
            }

            if (!string.IsNullOrWhiteSpace(data.Password))
                user.Password = data.Password;

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> DeleteUserAsync(string username)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null) return false;

            if (user.IsAdmin())
                throw new InvalidOperationException("Admin cannot be deleted");

            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> UserExistsAsync(string username)
        {
            return await _context.AppUsers.AnyAsync(u =>
                u.UserName.ToLower() == username.ToLower());
        }

        public async Task<bool> SubscribeAsync(int userId, int channelId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return false;

            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Id == channelId);

            if (channel == null)
                return false;

            if (!user.SubscribedChannels.Any(c => c.Id == channelId))
            {
                user.SubscribedChannels.Add(channel);
            }

            var articlesInChannel = await _context.Articles
                .Where(a => a.RSSChannelId == channelId)
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
            return true;
        }

        public async Task<bool> UnsubscribeAsync(int userId, int channelId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return false;

            var subscription = user.SubscribedChannels
                .FirstOrDefault(s => s.Id == channelId);

            if (subscription == null)
                return false;

            user.SubscribedChannels.Remove(subscription);

            var articleIds = await _context.Articles
                .Where(a => a.RSSChannelId == channelId)
                .Select(a => a.Id)
                .ToListAsync();

            var statesToRemove = await _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == user.Id &&
                    articleIds.Contains(s.ArticleId) &&
                    !s.IsFavorite)
                .ToListAsync();

            _context.UserArticleStates.RemoveRange(statesToRemove);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SubscribeAsync(string username, int channelId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return false;

            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Id == channelId);

            if (channel == null)
                return false;

            if (!user.SubscribedChannels.Any(c => c.Id == channelId))
            {
                user.SubscribedChannels.Add(channel);
            }

            var articlesInChannel = await _context.Articles
                .Where(a => a.RSSChannelId == channelId)
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

            return true;
        }

        public async Task<bool> UnsubscribeAsync(string username, int channelId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return false;

            var subscription = user.SubscribedChannels
                .FirstOrDefault(s => s.Id == channelId);

            if (subscription == null)
                return false;

            user.SubscribedChannels.Remove(subscription);

            var articleIds = await _context.Articles
                .Where(a => a.RSSChannelId == channelId)
                .Select(a => a.Id)
                .ToListAsync();

            var statesToRemove = await _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == user.Id &&
                    articleIds.Contains(s.ArticleId) &&
                    !s.IsFavorite)
                .ToListAsync();

            _context.UserArticleStates.RemoveRange(statesToRemove);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<RSSChannel>> GetSubscriptionsAsync(string username)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            return user?.SubscribedChannels ?? new List<RSSChannel>();
        }

        private static string NormalizeUrlString(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            url = url.Trim().ToLowerInvariant();
            return url.TrimEnd('/');
        }

        public async Task<List<RSSChannel>> GetSubscriptionsAsync(int userId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user?.SubscribedChannels ?? new List<RSSChannel>();
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            url = url.Trim().ToLowerInvariant();
            return url.TrimEnd('/');
        }
    }
}
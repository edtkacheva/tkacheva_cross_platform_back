using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class ArticleService
    {
        private readonly ApplicationDbContext _context;

        public ArticleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Article>> GetAllArticlesAsync()
        {
            return await _context.Articles
                .Include(a => a.RSSChannel)
                .OrderByDescending(a => a.PublishedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Article?> GetArticleByTitleAsync(string title)
        {
            return await _context.Articles
                .Include(a => a.RSSChannel)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));
        }

        public async Task<Article> CreateArticleAsync(Article article, string channelName)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(r => r.Name.ToLower() == channelName.ToLower());

            if (channel == null)
                throw new ArgumentException("Channel not found");

            if (!article.IsValidUrl())
                throw new ArgumentException("Invalid URL");

            article.RSSChannelId = channel.Id;
            article.RSSChannel = channel;
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return article;
        }

        public async Task<Article?> UpdateArticleAsync(string title, string? newTitle, string? url, string? channelName, DateTime? publishedAt, string? Description)
        {
            var article = await _context.Articles
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));

            if (article == null) return null;

            if (!string.IsNullOrWhiteSpace(channelName))
            {
                var channel = await _context.RSSChannels
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

                if (channel == null)
                    throw new ArgumentException("Channel not found");

                article.RSSChannelId = channel.Id;
                article.RSSChannel = channel;
            }

            article.Title = newTitle ?? article.Title;
            article.Url = url ?? article.Url;

            if (publishedAt != null)
                article.PublishedAt = publishedAt.Value;

            await _context.SaveChangesAsync();
            return article;
        }

        public async Task<bool> DeleteArticleAsync(string title)
        {
            var article = await _context.Articles
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));

            if (article == null) return false;

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Article>> SearchByDescriptionAsync(string text)
        {
            text = text.Trim();

            return await _context.Articles
                .Include(a => a.RSSChannel)
                .Where(a => a.Description != null &&
                            EF.Functions.Like(a.Description, $"%{text}%"))
                .ToListAsync();
        }

        public async Task<List<Article>> GetUnreadArticlesForUserAsync(string username)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var states = await _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == user.Id &&
                    !s.IsRead &&
                    s.Article != null &&
                    subscribedChannelIds.Contains(s.Article.RSSChannelId))
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(s =>
                {
                    var article = s.Article!;
                    article.IsRead = s.IsRead;
                    article.IsFavorite = s.IsFavorite;
                    return article;
                })
                .ToList();
        }

        public async Task<List<Article>> GetArticlesForUserAsync(int userId, int page, int pageSize)
        {
            if (page < 1)
                page = 1;

            if (pageSize < 1)
                pageSize = 10;

            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var states = await _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == user.Id &&
                    s.Article != null &&
                    subscribedChannelIds.Contains(s.Article.RSSChannelId))
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(s =>
                {
                    var article = s.Article!;
                    article.IsRead = s.IsRead;
                    article.IsFavorite = s.IsFavorite;
                    return article;
                })
                .ToList();
        }

        public async Task<List<Article>> GetArticlesForUserAsync(
            int userId,
            bool isRead,
            int page,
            int pageSize,
            DateTime? readBefore = null)
        {
            if (page < 1)
                page = 1;

            if (pageSize < 1)
                pageSize = 10;

            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var query = _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == userId &&
                    s.IsRead == isRead &&
                    s.Article != null &&
                    subscribedChannelIds.Contains(s.Article.RSSChannelId));

            if (isRead && readBefore != null)
            {
                query = query.Where(s =>
                    s.ReadAt == null || s.ReadAt < readBefore.Value);
            }

            var states = await query
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(s =>
                {
                    var article = s.Article!;
                    article.IsRead = s.IsRead;
                    article.IsFavorite = s.IsFavorite;
                    return article;
                })
                .ToList();
        }

        public async Task<bool> MarkAsReadAsync(int userId, int articleId)
        {
            var state = await _context.UserArticleStates
                .FirstOrDefaultAsync(s =>
                    s.AppUserId == userId &&
                    s.ArticleId == articleId);

            if (state == null)
                return false;

            if (!state.IsRead)
            {
                state.IsRead = true;
                state.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> SetFavoriteAsync(int userId, int articleId, bool isFavorite)
        {
            var userExists = await _context.AppUsers
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
                return false;

            var articleExists = await _context.Articles
                .AnyAsync(a => a.Id == articleId);

            if (!articleExists)
                return false;

            var state = await _context.UserArticleStates
                .FirstOrDefaultAsync(s =>
                    s.AppUserId == userId &&
                    s.ArticleId == articleId);

            if (state == null)
            {
                if (!isFavorite)
                    return true;

                state = new UserArticleState
                {
                    AppUserId = userId,
                    ArticleId = articleId,
                    IsRead = false,
                    IsFavorite = true,
                    AddedAt = DateTime.UtcNow
                };

                _context.UserArticleStates.Add(state);
            }
            else
            {
                state.IsFavorite = isFavorite;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        //public async Task<bool> MarkAsReadAsync(string username, int articleId)
        //{
        //    var state = await _context.UserArticleStates
        //        .Include(x => x.AppUser)
        //        .FirstOrDefaultAsync(x =>
        //            x.AppUser!.UserName.ToLower() == username.ToLower() &&
        //            x.ArticleId == articleId);

        //    if (state == null)
        //        return false;

        //    state.IsRead = true;
        //    state.ReadAt = DateTime.UtcNow;
        //    await _context.SaveChangesAsync();
        //    return true;
        //}

        public async Task<List<Article>> GetFavoriteArticlesForUserAsync(string username)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return new List<Article>();

            var states = await _context.UserArticleStates
                .Where(s => s.AppUserId == user.Id && s.IsFavorite)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(s =>
                {
                    var article = s.Article!;
                    article.IsRead = s.IsRead;
                    article.IsFavorite = s.IsFavorite;
                    return article;
                })
                .ToList();
        }

        public async Task<List<Article>> GetFavoriteArticlesForUserAsync(int userId)
        {
            var userExists = await _context.AppUsers
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
                return new List<Article>();

            var states = await _context.UserArticleStates
                .Where(s => s.AppUserId == userId && s.IsFavorite)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(s =>
                {
                    var article = s.Article!;
                    article.IsRead = s.IsRead;
                    article.IsFavorite = s.IsFavorite;
                    return article;
                })
                .ToList();
        }

        public async Task<bool> SetFavoriteAsync(string username, int articleId, bool isFavorite)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return false;

            var article = await _context.Articles
                .FirstOrDefaultAsync(a => a.Id == articleId);

            if (article == null)
                return false;

            var state = await _context.UserArticleStates
                .FirstOrDefaultAsync(s => s.AppUserId == user.Id && s.ArticleId == articleId);

            if (state == null)
            {
                if (!isFavorite)
                    return true;

                state = new UserArticleState
                {
                    AppUserId = user.Id,
                    ArticleId = articleId,
                    IsRead = false,
                    IsFavorite = true,
                    AddedAt = DateTime.UtcNow
                };

                _context.UserArticleStates.Add(state);
            }
            else
            {
                state.IsFavorite = isFavorite;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAsReadAsync(string username, int articleId)
        {
            Console.WriteLine($"Получен запрос на пометку статьи с ID {articleId} для пользователя {username}");

            var state = await _context.UserArticleStates
                .Include(x => x.AppUser)
                .FirstOrDefaultAsync(x => x.AppUser.UserName.ToLower() == username.ToLower() && x.ArticleId == articleId);

            // Логирование, чтобы проверить, что мы нашли статью
            if (state == null)
            {
                Console.WriteLine($"Статья с ID {articleId} не найдена для пользователя {username}");
                return false;
            }

            // Если статья уже помечена как прочитанная
            if (state.IsRead)
            {
                Console.WriteLine($"Статья с ID {articleId} уже прочитана.");
                return true;
            }

            // Обновляем статус статьи
            state.IsRead = true;
            state.ReadAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(); // Сохраняем изменения в базе
                Console.WriteLine($"Статья с ID {articleId} помечена как прочитанная для пользователя {username}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при пометке статьи с ID {articleId}: {ex.Message}");
                return false;
            }
        }
    }
}
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
            return await _context.UserArticleStates
                .Where(x => x.AppUser!.UserName.ToLower() == username.ToLower() && !x.IsRead)
                .Include(x => x.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .OrderByDescending(x => x.AddedAt)
                .Select(x => x.Article!)
                .ToListAsync();
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
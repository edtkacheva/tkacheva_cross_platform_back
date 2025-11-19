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
            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return article;
        }

        public async Task<Article?> UpdateArticleAsync(string title, string? newTitle, string? url, string? channelName, DateTime? publishedAt)
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
    }
}
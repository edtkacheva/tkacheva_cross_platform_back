using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Net;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class ArticleCategoryService
    {
        private readonly ApplicationDbContext _context;

        public ArticleCategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task EnsureCategoriesAsync(int articleId, IEnumerable<string>? categoryNames)
        {
            if (articleId <= 0 || categoryNames == null)
                return;

            var preparedCategories = categoryNames
                .Select(NormalizeName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new
                {
                    Name = name,
                    NormalizedName = NormalizeForSearch(name)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName))
                .ToList();

            if (preparedCategories.Count == 0)
                return;

            var existingNormalizedNames = await _context.ArticleCategories
                .Where(c => c.ArticleId == articleId)
                .Select(c => c.NormalizedName)
                .ToListAsync();

            var existingSet = existingNormalizedNames.ToHashSet();

            foreach (var category in preparedCategories)
            {
                if (existingSet.Contains(category.NormalizedName))
                    continue;

                _context.ArticleCategories.Add(new ArticleCategory
                {
                    ArticleId = articleId,
                    Name = category.Name,
                    NormalizedName = category.NormalizedName
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<ArticleCategory>> GetAllCategoriesAsync()
        {
            return await _context.ArticleCategories
                .GroupBy(c => c.NormalizedName)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<ArticleCategory>> GetCategoriesForUserAsync(int userId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<ArticleCategory>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            return await _context.ArticleCategories
                .Where(c =>
                    c.Article != null &&
                    subscribedChannelIds.Contains(c.Article.RSSChannelId))
                .GroupBy(c => c.NormalizedName)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        public static string NormalizeForSearch(string? text)
        {
            text = NormalizeName(text);

            if (string.IsNullOrWhiteSpace(text))
                return "";

            return text.ToLowerInvariant();
        }

        private static string NormalizeName(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, "<.*?>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }
    }
}
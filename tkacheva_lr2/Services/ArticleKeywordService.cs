using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class ArticleKeywordService
    {
        private readonly ApplicationDbContext _context;

        public ArticleKeywordService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task EnsureBaseKeywordsAsync(Article article)
        {
            if (article.Id <= 0)
                return;

            await EnsureKeywordAsync(article.Id, article.Title, "Title", 1.0);

            if (!string.IsNullOrWhiteSpace(article.Description))
            {
                await EnsureKeywordAsync(article.Id, article.Description, "Description", 0.8);
            }
        }

        public async Task ReplaceAiKeywordsAsync(int articleId, List<KeywordDto> keywords)
        {
            var oldAiKeywords = await _context.ArticleKeywords
                .Where(k => k.ArticleId == articleId && k.Source == "AI")
                .ToListAsync();

            _context.ArticleKeywords.RemoveRange(oldAiKeywords);

            foreach (var keyword in keywords)
            {
                var text = NormalizeText(keyword.Text);

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                _context.ArticleKeywords.Add(new ArticleKeyword
                {
                    ArticleId = articleId,
                    Text = text,
                    NormalizedText = NormalizeForSearch(text),
                    Source = "AI",
                    Weight = keyword.Weight,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task EnsureBaseKeywordsForAllArticlesAsync()
        {
            var articles = await _context.Articles
                .AsNoTracking()
                .ToListAsync();

            foreach (var article in articles)
            {
                await EnsureBaseKeywordsAsync(article);
            }
        }

        private async Task EnsureKeywordAsync(
            int articleId,
            string? text,
            string source,
            double weight)
        {
            text = NormalizeText(text);

            if (string.IsNullOrWhiteSpace(text))
                return;

            var exists = await _context.ArticleKeywords.AnyAsync(k =>
                k.ArticleId == articleId &&
                k.Source == source);

            if (exists)
                return;

            _context.ArticleKeywords.Add(new ArticleKeyword
            {
                ArticleId = articleId,
                Text = text,
                NormalizedText = NormalizeForSearch(text),
                Source = source,
                Weight = weight,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public static string NormalizeForSearch(string? text)
        {
            text = NormalizeText(text);

            if (string.IsNullOrWhiteSpace(text))
                return "";

            return text.ToLowerInvariant();
        }

        private static string NormalizeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = HtmlEntity.DeEntitize(text);
            text = Regex.Replace(text, "<.*?>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }
    }
}
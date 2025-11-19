using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class ReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<object?> GetArticlesByChannelAsync(string channelName)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));

            if (channel == null) return null;

            var articles = await _context.Articles
                .Where(a => a.RSSChannelId == channel.Id)
                .ToListAsync();

            return new
            {
                Channel = channel.Name,
                Count = articles.Count,
                Articles = articles
            };
        }

        public async Task<List<object>> GetChannelCountsAsync()
        {
            var result = await _context.RSSChannels
                .Include(c => c.Articles)
                .Select(c => new
                {
                    Channel = c.Name,
                    Count = c.Articles.Count
                })
                .ToListAsync();

            return result.Cast<object>().ToList();
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/report")]
    public class ReportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("by-channel/{channelName}")]
        public IActionResult GetArticlesByChannel(string channelName)
        {
            var channel = _context.RSSChannels
                .FirstOrDefault(c => c.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));

            if (channel == null)
                return NotFound("Channel not found");

            var articles = _context.Articles
                .Where(a => a.RSSChannelId == channel.Id)
                .ToList();

            return Ok(new
            {
                Channel = channel.Name,
                Count = articles.Count,
                Articles = articles
            });
        }

        [HttpGet("counts")]
        public IActionResult GetCounts()
        {
            var result = _context.RSSChannels
                .Include(c => c.Articles)
                .Select(c => new
                {
                    Channel = c.Name,
                    Count = c.Articles.Count
                })
                .ToList();

            return Ok(result);
        }
    }
}

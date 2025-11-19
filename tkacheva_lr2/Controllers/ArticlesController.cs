using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/articles")]
    public class ArticlesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ArticlesController(ApplicationDbContext context) => _context = context;

        // GET api/articles - все статьи (только авторизованные)
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Article>>> GetAll()
        {
            var list = await _context.Articles
                .Include(a => a.RSSChannel)
                .AsNoTracking()
                .ToListAsync();
            return Ok(list);
        }

        // GET api/articles/{title}
        [HttpGet("{title}")]
        [Authorize]
        public async Task<IActionResult> GetByTitle(string title)
        {
            var article = await _context.Articles
                .Where(a => EF.Functions.Like(a.Title, title))
                .FirstOrDefaultAsync();

            if (article == null)
                return NotFound($"Article with title '{title}' not found");

            return Ok(article);
        }

        // POST api/articles - создаем статью; в теле: Title, Url, ChannelName
        [HttpPost]
        [Authorize(Roles = "Admin")] // добавление только для Admin
        public async Task<ActionResult> Create([FromBody] ArticleCreateRequest req)
        {
            var channel = await _context.RSSChannels
                .Where(r => EF.Functions.Like(r.Name.ToLower(), req.ChannelName.ToLower()))
                .FirstOrDefaultAsync();
            if (channel == null) return BadRequest("Channel not found");

            var article = new Article
            {
                Title = req.Title,
                Url = req.Url,
                PublishedAt = req.PublishedAt == default ? DateTime.UtcNow : req.PublishedAt,
                RSSChannelId = channel.Id
            };

            if (!article.IsValidUrl()) return BadRequest("Invalid URL");

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByTitle), new { title = article.Title }, article);
        }

        // PUT api/articles/{title} - Admin
        [HttpPut("{title}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(string title, [FromBody] ArticleUpdateRequest req)
        {
            var art = await _context.Articles
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));

            if (art == null) return NotFound();

            // Если сменили канал по имени:
            if (!string.IsNullOrWhiteSpace(req.ChannelName))
            {
                var ch = await _context.RSSChannels
                    .FirstOrDefaultAsync(c => EF.Functions.Like(c.Name, req.ChannelName));

                if (ch == null)
                    return BadRequest("Channel not found");

                art.RSSChannelId = ch.Id;
            }

            art.Title = req.Title ?? art.Title;
            art.Url = req.Url ?? art.Url;

            if (req.PublishedAt != null)
                art.PublishedAt = req.PublishedAt.Value;

            await _context.SaveChangesAsync();
            return Ok(art);
        }

        // DELETE api/articles/{title} - Admin
        [HttpDelete("{title}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(string title)
        {
            var art = await _context.Articles
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));
            if (art == null) return NotFound();

            _context.Articles.Remove(art);
            await _context.SaveChangesAsync();
            return Ok("Article deleted");
        }
    }

    public class ArticleCreateRequest
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string ChannelName { get; set; } = "";
        public DateTime PublishedAt { get; set; } = default;
    }

    public class ArticleUpdateRequest
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? ChannelName { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
}

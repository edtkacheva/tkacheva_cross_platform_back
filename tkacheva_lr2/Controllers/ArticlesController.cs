using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArticlesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ArticlesController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET api/articles
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var articles = await _db.Articles
                .Include(a => a.Category)
                .ToListAsync();

            return Ok(articles);
        }

        // GET api/articles/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var article = await _db.Articles
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null)
                return NotFound();

            return Ok(article);
        }

        // POST api/articles
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Article article)
        {
            _db.Articles.Add(article);
            await _db.SaveChangesAsync();
            return Ok(article);
        }

        // PUT api/articles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Article updated)
        {
            var article = await _db.Articles.FindAsync(id);
            if (article == null) return NotFound();

            article.Title = updated.Title;
            article.Url = updated.Url;
            article.PublishedAt = updated.PublishedAt;
            article.CategoryId = updated.CategoryId;

            await _db.SaveChangesAsync();
            return Ok(article);
        }

        // DELETE api/articles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _db.Articles.FindAsync(id);
            if (article == null) return NotFound();

            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // EXTRA: поиск по названию
        // GET api/articles/search?title=physics
        [HttpGet("search")]
        public async Task<IActionResult> Search(string title)
        {
            var result = await _db.Articles
                .Where(a => a.Title.Contains(title))
                .ToListAsync();

            return Ok(result);
        }
    }
}

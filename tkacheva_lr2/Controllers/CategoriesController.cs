using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CategoriesController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET api/categories
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _db.Categories.ToListAsync();
            return Ok(categories);
        }

        // GET api/categories/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null)
                return NotFound();

            return Ok(category);
        }

        // POST api/categories
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Category category)
        {
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
            return Ok(category);
        }

        // PUT api/categories/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Category updated)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();

            category.Name = updated.Name;
            category.Description = updated.Description;

            await _db.SaveChangesAsync();
            return Ok(category);
        }

        // DELETE api/categories/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // EXTRA: список статей категории
        // GET api/categories/5/articles
        [HttpGet("{id}/articles")]
        public async Task<IActionResult> GetArticlesOfCategory(int id)
        {
            var articles = await _db.Articles
                .Where(a => a.CategoryId == id)
                .ToListAsync();

            return Ok(articles);
        }
    }
}

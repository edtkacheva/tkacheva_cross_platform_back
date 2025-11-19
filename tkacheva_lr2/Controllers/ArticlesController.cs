using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/articles")]
    public class ArticlesController : ControllerBase
    {
        private readonly ArticleService _articleService;

        public ArticlesController(ArticleService articleService)
        {
            _articleService = articleService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Article>>> GetAll()
        {
            var articles = await _articleService.GetAllArticlesAsync();
            return Ok(articles);
        }

        [HttpGet("{title}")]
        [Authorize]
        public async Task<IActionResult> GetByTitle(string title)
        {
            var article = await _articleService.GetArticleByTitleAsync(title);
            if (article == null)
                return NotFound($"Article with title '{title}' not found");

            return Ok(article);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Create([FromBody] ArticleCreateRequest req)
        {
            var article = new Article
            {
                Title = req.Title,
                Url = req.Url,
                PublishedAt = req.PublishedAt == default ? DateTime.UtcNow : req.PublishedAt
            };

            try
            {
                var createdArticle = await _articleService.CreateArticleAsync(article, req.ChannelName);
                return CreatedAtAction(nameof(GetByTitle), new { title = createdArticle.Title }, createdArticle);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{title}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(string title, [FromBody] ArticleUpdateRequest req)
        {
            try
            {
                var article = await _articleService.UpdateArticleAsync(
                    title, req.Title, req.Url, req.ChannelName, req.PublishedAt);

                if (article == null) return NotFound();
                return Ok(article);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{title}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(string title)
        {
            var result = await _articleService.DeleteArticleAsync(title);
            if (!result) return NotFound();
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;
using System.Text.Json;

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
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<Article>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? channelIds = null,
            [FromQuery] string? channelCategoryFilters = null,
            [FromQuery] string? sortOrder = "newest",
            [FromQuery] string? periodFilter = "all")
        {
            var selectedChannelIds = ParseIds(channelIds);
            var selectedChannelCategoryFilters = ParseChannelCategoryFilters(channelCategoryFilters);

            var articles = await _articleService.GetAllArticlesAsync(
                page,
                pageSize,
                search,
                selectedChannelCategoryFilters,
                sortOrder,
                periodFilter
            );

            return Ok(articles);
        }

        [HttpGet("categories")]
        [Authorize]
        public async Task<IActionResult> GetCategories()
        {
            if (User.IsInRole("Admin"))
            {
                var allCategories = await _articleService.GetAllCategoriesAsync();
                return Ok(allCategories);
            }

            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var categories = await _articleService.GetCategoriesForUserAsync(userId.Value);

            return Ok(categories);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyArticles(
            [FromQuery] bool isRead,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? channelIds = null,
            [FromQuery] string? channelCategoryFilters = null,
            [FromQuery] string? sortOrder = "newest",
            [FromQuery] string? periodFilter = "all")
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var selectedChannelIds = ParseIds(channelIds);
            var selectedChannelCategoryFilters = ParseChannelCategoryFilters(channelCategoryFilters);

            var result = await _articleService.GetArticlesForUserAsync(
                userId.Value,
                isRead,
                page,
                pageSize,
                search,
                selectedChannelCategoryFilters,
                sortOrder,
                periodFilter);

            return Ok(result);
        }

        [HttpGet("me/favorites")]
        [Authorize]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var result = await _articleService.GetFavoriteArticlesForUserAsync(userId.Value);

            return Ok(result);
        }

        [HttpGet("me/recommendations")]
        [Authorize]
        public async Task<IActionResult> GetMyRecommendations()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var result = await _articleService.GetRecommendationsForUserAsync(userId.Value);

            return Ok(result);
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
                PublishedAt = req.PublishedAt == default
                    ? DateTime.UtcNow
                    : req.PublishedAt,
                Description = req.Description
            };

            try
            {
                var createdArticle = await _articleService.CreateArticleAsync(article, req.ChannelName);

                return CreatedAtAction(
                    nameof(GetByTitle),
                    new { title = createdArticle.Title },
                    createdArticle
                );
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
                    title,
                    req.Title,
                    req.Url,
                    req.ChannelName,
                    req.PublishedAt,
                    req.Description
                );

                if (article == null)
                    return NotFound();

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

            if (!result)
                return NotFound();

            return Ok("Article deleted");
        }

        [HttpGet("search/description/{text}")]
        [Authorize]
        public async Task<IActionResult> SearchByDescription(string text)
        {
            var result = await _articleService.SearchByDescriptionAsync(text);

            return Ok(result);
        }

        [HttpGet("unread/{username}")]
        [Authorize]
        public async Task<IActionResult> GetUnreadForUser(string username)
        {
            var requester = User.Identity?.Name;

            if (requester == null)
                return Unauthorized();

            if (!User.IsInRole("Admin") && requester.ToLower() != username.ToLower())
                return Forbid();

            var result = await _articleService.GetUnreadArticlesForUserAsync(username);

            return Ok(result);
        }

        [HttpGet("favorites/{username}")]
        [Authorize]
        public async Task<IActionResult> GetFavoritesForUser(string username)
        {
            var requester = User.Identity?.Name;

            if (requester == null)
                return Unauthorized();

            if (!User.IsInRole("Admin") && requester.ToLower() != username.ToLower())
                return Forbid();

            var result = await _articleService.GetFavoriteArticlesForUserAsync(username);

            return Ok(result);
        }

        [HttpPost("{articleId:int}/mark-read")]
        [Authorize]
        public async Task<IActionResult> MarkAsRead(int articleId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var ok = await _articleService.MarkAsReadAsync(userId.Value, articleId);

            if (!ok)
                return NotFound(new { message = "Статья не найдена у пользователя." });

            return Ok(new { message = "Статья отмечена как прочитанная." });
        }

        [HttpPost("{articleId:int}/favorite")]
        [Authorize]
        public async Task<IActionResult> AddToFavorites(int articleId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var ok = await _articleService.SetFavoriteAsync(userId.Value, articleId, true);

            if (!ok)
                return NotFound(new { message = "Статья или пользователь не найдены." });

            return Ok(new { message = "Статья добавлена в избранное." });
        }

        [HttpDelete("{articleId:int}/favorite")]
        [Authorize]
        public async Task<IActionResult> RemoveFromFavorites(int articleId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var ok = await _articleService.SetFavoriteAsync(userId.Value, articleId, false);

            if (!ok)
                return NotFound(new { message = "Статья или пользователь не найдены." });

            return Ok(new { message = "Статья удалена из избранного." });
        }

        private int? GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out var userId))
                return null;

            return userId;
        }

        private static List<int> ParseIds(string? ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return new List<int>();

            return ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
        }

        private static List<string> ParseNames(string? names)
        {
            if (string.IsNullOrWhiteSpace(names))
                return new List<string>();

            return names
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Uri.UnescapeDataString(x).Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
        }

        [HttpGet("category-tree")]
        [Authorize]
        public async Task<IActionResult> GetCategoryTree()
        {
            if (User.IsInRole("Admin"))
            {
                var adminTree = await _articleService.GetChannelCategoryTreeAsync(null, true);
                return Ok(adminTree);
            }

            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var tree = await _articleService.GetChannelCategoryTreeAsync(userId.Value, false);

            return Ok(tree);
        }

        private static List<ChannelCategoryFilterDto> ParseChannelCategoryFilters(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<ChannelCategoryFilterDto>();

            try
            {
                var filters = JsonSerializer.Deserialize<List<ChannelCategoryFilterDto>>(
                    raw,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (filters == null)
                    return new List<ChannelCategoryFilterDto>();

                return filters
                    .Where(f => f.ChannelId > 0)
                    .Select(f => new ChannelCategoryFilterDto
                    {
                        ChannelId = f.ChannelId,
                        CategoryNames = (f.CategoryNames ?? new List<string>())
                            .Select(x => Uri.UnescapeDataString(x).Trim().ToLowerInvariant())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct()
                            .ToList()
                    })
                    .ToList();
            }
            catch
            {
                return new List<ChannelCategoryFilterDto>();
            }
        }
    }

    public class ArticleCreateRequest
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string ChannelName { get; set; } = "";
        public DateTime PublishedAt { get; set; } = default;
        public string? Description { get; set; } = "";
    }

    public class ArticleUpdateRequest
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? ChannelName { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? Description { get; set; }
    }
}
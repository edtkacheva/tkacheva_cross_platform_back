using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Services;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/ai")]
    public class AIController : ControllerBase
    {
        private readonly AIService _aiService;

        public AIController(AIService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("extract-keywords")]
        [Authorize]
        public async Task<IActionResult> ExtractKeywords([FromBody] ExtractKeywordsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(new { message = "Ссылка на статью не указана." });

            try
            {
                var keywords = await _aiService.ExtractKeywordsFromUrlAsync(request.Url);

                return Ok(new
                {
                    keywords
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                return BadRequest(new
                {
                    message = "Не удалось открыть ссылку на статью.",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Ошибка при анализе статьи.",
                    details = ex.Message
                });
            }
        }
    }

    public class ExtractKeywordsRequest
    {
        public string Url { get; set; } = "";
    }
}
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Services;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/report")]
    public class ReportController : ControllerBase
    {
        private readonly ReportService _reportService;

        public ReportController(ReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("by-channel/{channelName}")]
        public async Task<IActionResult> GetArticlesByChannel(string channelName)
        {
            var report = await _reportService.GetArticlesByChannelAsync(channelName);
            if (report == null)
                return NotFound("Channel not found");

            return Ok(report);
        }

        [HttpGet("counts")]
        public async Task<IActionResult> GetCounts()
        {
            var result = await _reportService.GetChannelCountsAsync();
            return Ok(result);
        }
    }
}
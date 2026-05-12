using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/rss")]
    public class RSSChannelsController : ControllerBase
    {
        private readonly RSSChannelService _channelService;

        public RSSChannelsController(RSSChannelService channelService)
        {
            _channelService = channelService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<RSSChannel>>> GetAll()
        {
            var channels = await _channelService.GetAllChannelsAsync();
            return Ok(channels);
        }

        [HttpGet("{name}")]
        [Authorize]
        public async Task<ActionResult<RSSChannel>> GetByName(string name)
        {
            var channel = await _channelService.GetChannelByNameAsync(name);
            if (channel == null) return NotFound();
            return Ok(channel);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromBody] CreateRSSChannelRequest request)
        {
            var username = User.Identity?.Name;
            if (username == null)
                return Unauthorized("Cannot determine current user.");

            try
            {
                var createdChannel = await _channelService.CreateChannelWithArticlesAsync(
                    request.Name,
                    request.Url,
                    username);

                return CreatedAtAction(nameof(GetByName), new { name = createdChannel.Name }, createdChannel);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(int id, [FromBody] CreateRSSChannelRequest request)
        {
            try
            {
                var channel = await _channelService.UpdateChannelAsync(id, request.Name, request.Url);

                if (channel == null)
                    return NotFound(new { message = "Канал не найден." });

                return Ok(channel);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("refresh")]
        [Authorize]
        public async Task<IActionResult> Refresh()
        {
            try
            {
                int addedArticlesCount;

                if (User.IsInRole("Admin"))
                {
                    addedArticlesCount = await _channelService.RefreshAllChannelsAsync();
                }
                else
                {
                    var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!int.TryParse(userIdString, out var userId))
                        return Unauthorized("Cannot determine current user id.");

                    addedArticlesCount = await _channelService.RefreshChannelsForUserAsync(userId);
                }

                return Ok(new
                {
                    message = "RSS-каналы обновлены.",
                    addedArticles = addedArticlesCount
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }


        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (!User.IsInRole("Admin"))
                return Forbid();

            var result = await _channelService.DeleteChannelAsync(id);

            if (!result)
                return NotFound(new { message = "Channel not found." });

            return Ok(new { message = "Channel deleted." });
        }
    }
}
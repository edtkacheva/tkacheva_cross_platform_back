using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;

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
        public async Task<ActionResult> Create([FromBody] RSSChannel channel)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can create channels.");

            try
            {
                var createdChannel = await _channelService.CreateChannelAsync(channel);
                return CreatedAtAction(nameof(GetByName), new { name = createdChannel.Name }, createdChannel);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{name}")]
        [Authorize]
        public async Task<ActionResult> Update(string name, [FromBody] RSSChannel updated)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can update channels.");

            try
            {
                var channel = await _channelService.UpdateChannelAsync(name, updated);
                if (channel == null) return NotFound();
                return Ok(channel);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpDelete("{name}")]
        [Authorize]
        public async Task<ActionResult> Delete(string name)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can delete channels.");

            var result = await _channelService.DeleteChannelAsync(name);
            if (!result) return NotFound();
            return Ok("Channel deleted");
        }
    }
}
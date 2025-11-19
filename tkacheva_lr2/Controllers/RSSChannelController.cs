using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/rss")]
    public class RSSChannelsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public RSSChannelsController(ApplicationDbContext context) => _context = context;


        // GET api/rss
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<RSSChannel>>> GetAll()
        {
            var list = await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
                .ToListAsync();
            return Ok(list);
        }


        // GET api/rss/{name}
        [HttpGet("{name}")]
        [Authorize]
        public async Task<ActionResult<RSSChannel>> GetByName(string name)
        {
            var ch = await _context.RSSChannels
                .Include(c => c.Articles)
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == name.ToLower());

            if (ch == null) return NotFound();
            return Ok(ch);
        }


        // POST api/rss
        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromBody] RSSChannel channel)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can create channels.");

            if (await _context.RSSChannels.AnyAsync(c =>
                c.Name.ToLower() == channel.Name.ToLower()))
                return Conflict("Channel name already exists.");

            _context.RSSChannels.Add(channel); // Id автогенерируется
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByName), new { name = channel.Name }, channel);
        }


        // PUT api/rss/{name}
        [HttpPut("{name}")]
        [Authorize]
        public async Task<ActionResult> Update(string name, [FromBody] RSSChannel updated)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can update channels.");

            var ch = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

            if (ch == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(updated.Name) &&
                !ch.Name.Equals(updated.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await _context.RSSChannels.AnyAsync(c =>
                    c.Name.ToLower() == updated.Name.ToLower()))
                    return Conflict("New channel name already exists.");

                ch.Name = updated.Name;
            }

            ch.Url = updated.Url;
            ch.Description = updated.Description;

            await _context.SaveChangesAsync();
            return Ok(ch);
        }


        // DELETE api/rss/{name}
        [HttpDelete("{name}")]
        [Authorize]
        public async Task<ActionResult> Delete(string name)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can delete channels.");

            var ch = await _context.RSSChannels
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

            if (ch == null) return NotFound();

            _context.RSSChannels.Remove(ch);
            await _context.SaveChangesAsync();

            return Ok("Channel deleted");
        }
    }
}

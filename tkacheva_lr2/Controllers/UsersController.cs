using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public UsersController(ApplicationDbContext context) => _context = context;


        // GET api/users
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<AppUser>>> GetAll()
        {
            return Ok(await _context.AppUsers.AsNoTracking().ToListAsync());
        }


        // GET api/users/{username}
        [HttpGet("{username}")]
        [Authorize]
        public async Task<ActionResult<AppUser>> GetByName(string username)
        {
            var user = await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return NotFound($"User '{username}' not found");

            return Ok(user);
        }


        // POST api/users — регистрация
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> Create([FromBody] AppUser user)
        {
            if (!user.IsPasswordStrong())
                return BadRequest("Password is too weak (min 6 chars).");

            if (await _context.AppUsers.AnyAsync(u =>
                u.UserName.ToLower() == user.UserName.ToLower()))
                return Conflict("UserName already exists.");

            // EF сам создаёт Id (AUTOINCREMENT)
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByName),
                new { username = user.UserName }, user);
        }


        // PUT api/users/{username}
        [HttpPut("{username}")]
        [Authorize]
        public async Task<ActionResult> Update(string username, [FromBody] AppUser data)
        {
            // Явная проверка роли
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can update users.");

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return NotFound("User not found.");

            if (!string.IsNullOrWhiteSpace(data.UserName))
            {
                var exists = await _context.AppUsers.AnyAsync(u =>
                    u.UserName.ToLower() == data.UserName.ToLower() &&
                    u.Id != user.Id);

                if (exists)
                    return Conflict("New username already taken.");

                user.UserName = data.UserName;
            }

            if (!string.IsNullOrWhiteSpace(data.Password))
                user.Password = data.Password;

            await _context.SaveChangesAsync();
            return Ok(user);
        }


        // DELETE api/users/{username}
        [HttpDelete("{username}")]
        [Authorize]
        public async Task<ActionResult> Delete(string username)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can delete users.");

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return NotFound();

            _context.AppUsers.Remove(user);
            await _context.SaveChangesAsync();

            return Ok("User deleted");
        }
    }
}

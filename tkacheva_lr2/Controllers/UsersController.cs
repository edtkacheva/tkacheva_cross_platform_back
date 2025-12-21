using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<AppUser>>> GetAll()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("{username}")]
        [Authorize]
        public async Task<ActionResult<AppUser>> GetByName(string username)
        {
            var requester = User.Identity?.Name;

            if (requester == null)
                return Unauthorized("Cannot determine current user.");

            // Админ может смотреть всех
            if (!User.IsInRole("Admin") && requester.ToLower() != username.ToLower())
                return Forbid("You can only view your own profile.");

            var user = await _userService.GetUserByNameAsync(username);
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> Create([FromBody] AppUser user)
        {
            try
            {
                var createdUser = await _userService.CreateUserAsync(user);
                return CreatedAtAction(nameof(GetByName), new { username = createdUser.UserName }, createdUser);
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

        [HttpPut("{username}")]
        [Authorize]
        public async Task<ActionResult> Update(string username, [FromBody] AppUser data)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can update users.");

            try
            {
                var user = await _userService.UpdateUserAsync(username, data);
                if (user == null)
                    return NotFound("User not found.");

                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpDelete("{username}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(string username)
        {
            if (!User.IsInRole("Admin"))
                return Forbid("Only admin can delete users.");

            var result = await _userService.DeleteUserAsync(username);
            if (!result)
                return NotFound();

            return Ok("User deleted");
        }

        [HttpPost("{username}/subscribe/{channelName}")]
        [Authorize]
        public async Task<ActionResult> Subscribe(string username, string channelName)
        {
            var ok = await _userService.SubscribeAsync(username, channelName);
            if (!ok) return NotFound();
            return Ok($"User {username} subscribed to {channelName}");
        }

        [HttpPost("{username}/unsubscribe/{channelName}")]
        [Authorize]
        public async Task<ActionResult> Unsubscribe(string username, string channelName)
        {
            var ok = await _userService.UnsubscribeAsync(username, channelName);
            if (!ok) return NotFound();
            return Ok($"User {username} unsubscribed from {channelName}");
        }

        [HttpGet("{username}/subscriptions")]
        [Authorize]
        public async Task<ActionResult> GetSubscriptions(string username)
        {
            var list = await _userService.GetSubscriptionsAsync(username);
            return Ok(list);
        }

    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Models;
using tkacheva_lr2.Services;
using System.Security.Claims;

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

        private int? GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out var userId))
                return null;

            return userId;
        }
        
        [HttpPost("me/subscribe/{channelId}")]
        [Authorize]
        public async Task<ActionResult> SubscribeCurrentUser(int channelId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var ok = await _userService.SubscribeAsync(userId.Value, channelId);

            if (!ok)
                return NotFound("User or channel not found.");

            return Ok(new { message = "Subscribed successfully." });
        }

        [HttpPost("me/unsubscribe/{channelId}")]
        [Authorize]
        public async Task<ActionResult> UnsubscribeCurrentUser(int channelId)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var ok = await _userService.UnsubscribeAsync(userId.Value, channelId);

            if (!ok)
                return NotFound("Subscription not found.");

            return Ok(new { message = "Unsubscribed successfully." });
        }

        [HttpGet("me/subscriptions")]
        [Authorize]
        public async Task<ActionResult> GetMySubscriptions()
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized("Cannot determine current user id.");

            var list = await _userService.GetSubscriptionsAsync(userId.Value);
            return Ok(list);
        }

    }
}
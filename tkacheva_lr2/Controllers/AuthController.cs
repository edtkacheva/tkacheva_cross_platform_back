using Microsoft.AspNetCore.Mvc;
using tkacheva_lr2.Services;

namespace tkacheva_lr2.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.AuthenticateAsync(request.UserName, request.Password);
            if (token == null)
                return Unauthorized("Incorrect login or password");

            return Ok(new { token });
        }
    }

    public class LoginRequest
    {
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
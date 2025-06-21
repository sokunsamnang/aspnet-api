using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using aspnet_core_api.Models;
using aspnet_core_api.Services;

namespace aspnet_core_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            var token = await _authService.LoginAsync(loginModel);
            if (token == null)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }
            return Ok(new { token });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LoginModel loginModel)
        {
            if (string.IsNullOrEmpty(loginModel.Username) || string.IsNullOrEmpty(loginModel.Password))
            {
                return BadRequest(new { message = "Username and password are required" });
            }

            var token = await _authService.RegisterAsync(loginModel);
            if (token == null)
            {
                return Conflict(new { message = "User already exists" });
            }

            return Ok(new { token, message = "User registered successfully" });
        }
    }
}
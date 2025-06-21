using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using aspnet_core_api.Models;
using aspnet_core_api.Data;

namespace aspnet_core_api.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string?> LoginAsync(LoginModel loginModel)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginModel.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginModel.Password, user.PasswordHash))
            {
                return null;
            }

            return GenerateJwtToken(user);
        }

        public Task LogoutAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<string?> RegisterAsync(LoginModel loginModel)
        {
            // Check if user already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginModel.Username);
            if (existingUser != null)
            {
                return null; // User already exists
            }

            // Create new user
            var user = new User
            {
                Username = loginModel.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(loginModel.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                // Fallback key for development
                jwtKey = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "api-gateway",
                audience: _configuration["Jwt:Audience"] ?? "microservices-users",
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:DurationInMinutes"] ?? "60")),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
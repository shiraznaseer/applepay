using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApplePay.Models;
using Microsoft.Extensions.Options;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly AuthUsersConfig _authUsersConfig;

        public AuthController(IConfiguration configuration, ILogger<AuthController> logger, IOptions<AuthUsersConfig> authUsersConfig)
        {
            _configuration = configuration;
            _logger = logger;
            _authUsersConfig = authUsersConfig.Value;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                // Authenticate user with username and password
                if (IsValidUser(request.Username, request.Password))
                {
                    var role = GetUserRole(request.Username);
                    var token = GenerateJwtToken(request.Username, role);
                    
                    return Ok(new { 
                        token = token,
                        expiresIn = 946080000, // 30 years in seconds
                        user = new { 
                            username = request.Username,
                            role = role
                        }
                    });
                }

                return Unauthorized(new { message = "Invalid username or password" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user: {Username}", request.Username);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("validate")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new {
                valid = true,
                userId = userId,
                role = role,
                expires = User.FindFirst("exp")?.Value
            });
        }

        private bool IsValidUser(string username, string password)
        {
            // Validate against configured users
            return IsValidConfiguredUser(_authUsersConfig.Admin, username, password) ||
                   IsValidConfiguredUser(_authUsersConfig.TestUser, username, password) ||
                   IsValidConfiguredUser(_authUsersConfig.ExternalUser, username, password);
        }

        private bool IsValidConfiguredUser(AuthUser user, string username, string password)
        {
            return user.Username == username && user.Password == password;
        }

        private string GetUserRole(string username)
        {
            if (username == _authUsersConfig.Admin.Username)
                return _authUsersConfig.Admin.Role;
            else if (username == _authUsersConfig.TestUser.Username)
                return _authUsersConfig.TestUser.Role;
            else if (username == _authUsersConfig.ExternalUser.Username)
                return _authUsersConfig.ExternalUser.Role;
            
            return "User"; // Default role
        }

        private string GenerateJwtToken(string userId, string role)
        {
            var jwtSettings = _configuration.GetSection("WebSocketAuth");
            var secretKey = jwtSettings["SecretKey"] ?? "default-secret-key-change-in-production";
            var issuer = jwtSettings["Issuer"] ?? "ApplePayAPI";
            var audience = jwtSettings["Audience"] ?? "ApplePayClients";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
                new Claim("jti", Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddYears(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

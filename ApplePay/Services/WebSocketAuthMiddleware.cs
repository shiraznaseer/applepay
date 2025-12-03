using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace ApplePay.Services
{
    public class WebSocketAuthOptions
    {
        public const string SectionName = "WebSocketAuth";
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public class WebSocketAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public WebSocketAuthMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest && context.Request.Path.StartsWithSegments("/ws"))
            {
                var token = GetTokenFromRequest(context.Request);
                
                if (string.IsNullOrEmpty(token) || !ValidateToken(token))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }
            }

            await _next(context);
        }

        private string? GetTokenFromRequest(HttpRequest request)
        {
            // Try to get token from Authorization header
            var authHeader = request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                return authHeader.Substring("Bearer ".Length);
            }

            // Try to get token from query string
            if (request.Query.ContainsKey("token"))
            {
                return request.Query["token"];
            }

            // Try to get token from cookie
            if (request.Cookies.ContainsKey("access_token"))
            {
                return request.Cookies["access_token"];
            }

            return null;
        }

        private bool ValidateToken(string token)
        {
            try
            {
                var jwtSettings = _configuration.GetSection(WebSocketAuthOptions.SectionName);
                var secretKey = jwtSettings["SecretKey"];
                
                if (string.IsNullOrEmpty(secretKey))
                {
                    // For development, allow all tokens if no secret is configured
                    return true;
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(secretKey);
                
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = !string.IsNullOrEmpty(jwtSettings["Issuer"]),
                    ValidateAudience = !string.IsNullOrEmpty(jwtSettings["Audience"]),
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

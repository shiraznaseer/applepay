using System.Collections.Concurrent;
using System.Net;
using Microsoft.IdentityModel.Tokens;

namespace ApplePay.Services
{
    public class WebSocketRateLimitOptions
    {
        public const string SectionName = "WebSocketRateLimit";
        public int MaxConnectionsPerIp { get; set; } = 10000;
        public int MaxConnectionsPerUser { get; set; } = 5000;
        public int MaxMessagesPerMinute { get; set; } = 10000;
        public int MaxConnectionAttemptsPerHour { get; set; } = 10000;
        public int BanDurationMinutes { get; set; } = 1;
    }

    public class WebSocketRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketRateLimitMiddleware> _logger;
        private readonly WebSocketRateLimitOptions _options;

        // Rate limiting stores
        private readonly ConcurrentDictionary<string, int> _ipConnectionCounts = new();
        private readonly ConcurrentDictionary<string, int> _userConnectionCounts = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _messageTimestamps = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _connectionAttempts = new();
        private readonly ConcurrentDictionary<string, DateTime> _bannedIps = new();

        private readonly Timer _cleanupTimer;

        public WebSocketRateLimitMiddleware(RequestDelegate next, ILogger<WebSocketRateLimitMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            
            _options = configuration.GetSection(WebSocketRateLimitOptions.SectionName).Get<WebSocketRateLimitOptions>() 
                     ?? new WebSocketRateLimitOptions();

            // Start cleanup timer to remove old entries
            _cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest && context.Request.Path.StartsWithSegments("/ws"))
            {
                var clientIp = GetClientIpAddress(context);
                var userId = ExtractUserIdFromRequest(context);

                // Check if IP is banned
                if (_bannedIps.TryGetValue(clientIp, out var banEndTime) && DateTime.UtcNow < banEndTime)
                {
                    _logger.LogWarning($"Banned IP {clientIp} attempted to connect");
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Too many connection attempts. Please try again later.");
                    return;
                }

                // Check connection attempt rate
                if (!CheckConnectionAttemptRate(clientIp))
                {
                    BanIp(clientIp);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Too many connection attempts. Please try again later.");
                    return;
                }

                // Check connection limits
                if (!CheckConnectionLimits(clientIp, userId))
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Connection limit exceeded");
                    return;
                }

                // Track connection
                TrackConnection(clientIp, userId);
            }

            await _next(context);
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ipAddress))
            {
                return ipAddress.Split(',')[0].Trim();
            }

            ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ipAddress))
            {
                return ipAddress;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string? ExtractUserIdFromRequest(HttpContext context)
        {
            var token = GetTokenFromRequest(context.Request);
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                var jwtSettings = context.RequestServices.GetRequiredService<IConfiguration>().GetSection("WebSocketAuth");
                var secretKey = jwtSettings["SecretKey"];
                
                if (string.IsNullOrEmpty(secretKey)) return null;

                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var key = System.Text.Encoding.ASCII.GetBytes(secretKey);
                
                tokenHandler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    ValidateIssuer = !string.IsNullOrEmpty(jwtSettings["Issuer"]),
                    ValidateAudience = !string.IsNullOrEmpty(jwtSettings["Audience"]),
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;
                return jwtToken.Claims.FirstOrDefault(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier || x.Type == "sub")?.Value;
            }
            catch
            {
                return null;
            }
        }

        private string? GetTokenFromRequest(HttpRequest request)
        {
            var authHeader = request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                return authHeader.Substring("Bearer ".Length);
            }

            if (request.Query.ContainsKey("token"))
            {
                return request.Query["token"];
            }

            if (request.Cookies.ContainsKey("access_token"))
            {
                return request.Cookies["access_token"];
            }

            return null;
        }

        private bool CheckConnectionAttemptRate(string clientIp)
        {
            var now = DateTime.UtcNow;
            var oneHourAgo = now.AddHours(-1);

            var attempts = _connectionAttempts.AddOrUpdate(clientIp, 
                new List<DateTime> { now },
                (key, existing) => 
                {
                    existing.Add(now);
                    return existing;
                });

            // Remove old attempts
            attempts.RemoveAll(timestamp => timestamp < oneHourAgo);

            return attempts.Count <= _options.MaxConnectionAttemptsPerHour;
        }

        private bool CheckConnectionLimits(string clientIp, string? userId)
        {
            var ipCount = _ipConnectionCounts.GetValueOrDefault(clientIp, 0);
            if (ipCount >= _options.MaxConnectionsPerIp)
            {
                _logger.LogWarning($"IP {clientIp} exceeded connection limit: {ipCount}/{_options.MaxConnectionsPerIp}");
                return false;
            }

            if (!string.IsNullOrEmpty(userId))
            {
                var userCount = _userConnectionCounts.GetValueOrDefault(userId, 0);
                if (userCount >= _options.MaxConnectionsPerUser)
                {
                    _logger.LogWarning($"User {userId} exceeded connection limit: {userCount}/{_options.MaxConnectionsPerUser}");
                    return false;
                }
            }

            return true;
        }

        private void TrackConnection(string clientIp, string? userId)
        {
            _ipConnectionCounts.AddOrUpdate(clientIp, 1, (key, value) => value + 1);

            if (!string.IsNullOrEmpty(userId))
            {
                _userConnectionCounts.AddOrUpdate(userId, 1, (key, value) => value + 1);
            }
        }

        private void BanIp(string clientIp)
        {
            var banEndTime = DateTime.UtcNow.AddMinutes(_options.BanDurationMinutes);
            _bannedIps.AddOrUpdate(clientIp, banEndTime, (key, value) => banEndTime);
            _logger.LogWarning($"IP {clientIp} banned until {banEndTime}");
        }

        private void CleanupOldEntries(object? state)
        {
            var now = DateTime.UtcNow;
            var oneHourAgo = now.AddHours(-1);
            var oneMinuteAgo = now.AddMinutes(-1);

            // Cleanup old connection attempts
            foreach (var kvp in _connectionAttempts)
            {
                kvp.Value.RemoveAll(timestamp => timestamp < oneHourAgo);
                if (kvp.Value.Count == 0)
                {
                    _connectionAttempts.TryRemove(kvp.Key, out _);
                }
            }

            // Cleanup old message timestamps
            foreach (var kvp in _messageTimestamps)
            {
                kvp.Value.RemoveAll(timestamp => timestamp < oneMinuteAgo);
                if (kvp.Value.Count == 0)
                {
                    _messageTimestamps.TryRemove(kvp.Key, out _);
                }
            }

            // Cleanup expired bans
            foreach (var kvp in _bannedIps)
            {
                if (kvp.Value < now)
                {
                    _bannedIps.TryRemove(kvp.Key, out _);
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}

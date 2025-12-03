using ApplePay.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace ApplePay.Services
{
    public class WebSocketHandler
    {
        private readonly IWebSocketNotificationService _notificationService;
        private readonly ILogger<WebSocketHandler> _logger;
        private readonly IConfiguration _configuration;

        public WebSocketHandler(IWebSocketNotificationService notificationService, ILogger<WebSocketHandler> logger, IConfiguration configuration)
        {
            _notificationService = notificationService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
        {
            var connectionId = Guid.NewGuid().ToString();
            var userId = ExtractUserIdFromToken(context);
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var ipAddress = GetClientIpAddress(context);

            await _notificationService.AddConnectionAsync(connectionId, webSocket, userId);

            // Send welcome message
            await SendWelcomeMessage(webSocket, connectionId);

            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result;

            try
            {
                _logger.LogInformation($"WebSocket connection established: {connectionId} for user: {userId} from {ipAddress}");

                while (webSocket.State == WebSocketState.Open)
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation($"WebSocket close request received: {connectionId}");
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await HandleTextMessage(webSocket, buffer, result, connectionId, userId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogWarning($"Binary message received from {connectionId}, not supported");
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, $"WebSocket error for connection {connectionId}: {ex.WebSocketErrorCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error for WebSocket connection {connectionId}");
            }
            finally
            {
                await _notificationService.RemoveConnectionAsync(connectionId);
                if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
                {
                    try
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error closing WebSocket connection {connectionId}");
                    }
                }
                _logger.LogInformation($"WebSocket connection closed: {connectionId}");
            }
        }

        private async Task SendWelcomeMessage(WebSocket webSocket, string connectionId)
        {
            try
            {
                var welcomeMessage = new
                {
                    type = "welcome",
                    connectionId = connectionId,
                    timestamp = DateTime.UtcNow,
                    message = "Connected to ApplePay WebSocket service"
                };

                var message = System.Text.Json.JsonSerializer.Serialize(welcomeMessage);
                var buffer = System.Text.Encoding.UTF8.GetBytes(message);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send welcome message to {connectionId}");
            }
        }

        private async Task HandleTextMessage(WebSocket webSocket, byte[] buffer, WebSocketReceiveResult result, string connectionId, string? userId)
        {
            try
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug($"Text message received from {connectionId}: {message}");

                // Parse and handle client messages (ping, subscribe, etc.)
                var messageObj = System.Text.Json.JsonDocument.Parse(message).RootElement;

                if (messageObj.TryGetProperty("type", out var typeProperty))
                {
                    var messageType = typeProperty.GetString();

                    switch (messageType?.ToLower())
                    {
                        case "ping":
                            await SendPongResponse(webSocket, connectionId);
                            break;
                        case "subscribe":
                            await HandleSubscription(webSocket, messageObj, connectionId, userId);
                            break;
                        case "unsubscribe":
                            await HandleUnsubscription(webSocket, messageObj, connectionId, userId);
                            break;
                        default:
                            _logger.LogWarning($"Unknown message type '{messageType}' from {connectionId}");
                            break;
                    }
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, $"Invalid JSON received from {connectionId}");
                await SendErrorMessage(webSocket, connectionId, "Invalid JSON format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling message from {connectionId}");
                await SendErrorMessage(webSocket, connectionId, "Error processing message");
            }
        }

        private async Task SendPongResponse(WebSocket webSocket, string connectionId)
        {
            try
            {
                var pongMessage = new
                {
                    type = "pong",
                    timestamp = DateTime.UtcNow
                };

                var message = System.Text.Json.JsonSerializer.Serialize(pongMessage);
                var buffer = System.Text.Encoding.UTF8.GetBytes(message);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send pong to {connectionId}");
            }
        }

        private async Task HandleSubscription(WebSocket webSocket, JsonElement messageObj, string connectionId, string? userId)
        {
            // Handle subscription to specific payment events or user-specific events
            _logger.LogInformation($"Subscription request from {connectionId} for user {userId}");
            
            var response = new
            {
                type = "subscription_confirmed",
                timestamp = DateTime.UtcNow,
                userId = userId
            };

            var message = System.Text.Json.JsonSerializer.Serialize(response);
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);

            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        private async Task HandleUnsubscription(WebSocket webSocket, JsonElement messageObj, string connectionId, string? userId)
        {
            _logger.LogInformation($"Unsubscription request from {connectionId} for user {userId}");
            
            var response = new
            {
                type = "unsubscription_confirmed",
                timestamp = DateTime.UtcNow
            };

            var message = System.Text.Json.JsonSerializer.Serialize(response);
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);

            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, buffer.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        private async Task SendErrorMessage(WebSocket webSocket, string connectionId, string error)
        {
            try
            {
                var errorMessage = new
                {
                    type = "error",
                    error = error,
                    timestamp = DateTime.UtcNow
                };

                var message = System.Text.Json.JsonSerializer.Serialize(errorMessage);
                var buffer = System.Text.Encoding.UTF8.GetBytes(message);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, buffer.Length),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send error message to {connectionId}");
            }
        }

        private string? ExtractUserIdFromToken(HttpContext context)
        {
            try
            {
                var token = GetTokenFromRequest(context.Request);
                if (string.IsNullOrEmpty(token)) return null;

                var jwtSettings = _configuration.GetSection("WebSocketAuth");
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
                return jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier || x.Type == "sub")?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract user ID from JWT token");
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
    }
}

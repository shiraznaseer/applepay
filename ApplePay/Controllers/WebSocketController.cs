using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebSocketController : ControllerBase
    {
        private readonly WebSocketHandler _webSocketHandler;
        private readonly IWebSocketNotificationService _notificationService;
        private readonly ILogger<WebSocketController> _logger;

        public WebSocketController(
            WebSocketHandler webSocketHandler,
            IWebSocketNotificationService notificationService,
            ILogger<WebSocketController> logger)
        {
            _webSocketHandler = webSocketHandler;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet("/ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await _webSocketHandler.HandleWebSocketAsync(HttpContext, webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await HttpContext.Response.WriteAsync("This endpoint accepts WebSocket connections only");
            }
        }

        [HttpGet("health")]
        public async Task<ActionResult<object>> HealthCheck()
        {
            try
            {
                var stats = await _notificationService.GetStatsAsync();
                var health = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    webSocket = new
                    {
                        totalConnections = stats.TotalConnections,
                        activeConnections = stats.ActiveConnections,
                        uniqueUsers = stats.UniqueUsers,
                        messagesSent = stats.MessagesSent,
                        messagesFailed = stats.MessagesFailed,
                        uptime = DateTime.UtcNow - stats.StartTime,
                        connectionsByUser = stats.ConnectionsByUser
                    },
                    server = new
                    {
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown",
                        machineName = Environment.MachineName
                    }
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message
                });
            }
        }

        [HttpGet("stats")]
        public async Task<ActionResult<WebSocketStats>> GetStats()
        {
            try
            {
                var stats = await _notificationService.GetStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get WebSocket stats");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpGet("connections")]
        public async Task<ActionResult<List<string>>> GetConnectedUsers()
        {
            try
            {
                var users = await _notificationService.GetConnectedUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get connected users");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpPost("test-notification")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> SendTestNotification([FromBody] TestNotificationRequest request)
        {
            try
            {
                var paymentEvent = new ApplePay.Models.PaymentUpdateEvent
                {
                    PaymentId = request.PaymentId ?? "test_pay_123",
                    OrderReferenceId = request.OrderReferenceId ?? "test_order_456",
                    Status = request.Status ?? "authorized",
                    Amount = request.Amount ?? 123.45m
                };

                if (!string.IsNullOrEmpty(request.UserId))
                {
                    await _notificationService.NotifyUserAsync(request.UserId, paymentEvent);
                }
                else
                {
                    await _notificationService.NotifyPaymentUpdateAsync(paymentEvent);
                }

                return Ok(new { message = "Test notification sent successfully", paymentEvent = paymentEvent });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test notification");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpGet("is-user-connected/{userId}")]
        public async Task<ActionResult<bool>> IsUserConnected(string userId)
        {
            try
            {
                var isConnected = await _notificationService.IsUserConnectedAsync(userId);
                return Ok(isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check if user {userId} is connected");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }

    public class TestNotificationRequest
    {
        public string? UserId { get; set; }
        public string? PaymentId { get; set; }
        public string? OrderReferenceId { get; set; }
        public string? Status { get; set; }
        public decimal? Amount { get; set; }
    }
}

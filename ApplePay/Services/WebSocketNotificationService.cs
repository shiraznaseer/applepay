using ApplePay.Models;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;

namespace ApplePay.Services
{
    public interface IWebSocketNotificationService
    {
        Task NotifyPaymentUpdateAsync(PaymentUpdateEvent paymentEvent);
        Task NotifyUserAsync(string userId, PaymentUpdateEvent paymentEvent);
        Task AddConnectionAsync(string connectionId, WebSocket webSocket, string? userId = null);
        Task RemoveConnectionAsync(string connectionId);
        Task<bool> IsUserConnectedAsync(string userId);
        Task<int> GetConnectionCountAsync();
        Task<List<string>> GetConnectedUsersAsync();
        Task<WebSocketStats> GetStatsAsync();
    }

    public class WebSocketStats
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int UniqueUsers { get; set; }
        public long MessagesSent { get; set; }
        public long MessagesFailed { get; set; }
        public DateTime StartTime { get; set; }
        public Dictionary<string, int> ConnectionsByUser { get; set; } = new();
    }

    public class WebSocketConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public WebSocket WebSocket { get; set; } = null!;
        public string? UserId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public string UserAgent { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public long MessagesSent { get; set; }
        public long MessagesFailed { get; set; }
    }

    public class WebSocketNotificationService : IWebSocketNotificationService
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, List<string>> _userConnections = new();
        private readonly ILogger<WebSocketNotificationService> _logger;
        private readonly WebSocketStats _stats;
        private readonly Timer _cleanupTimer;
        private readonly ConcurrentQueue<PaymentUpdateEvent> _messageQueue = new();
        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private bool _isProcessingQueue = false;

        public WebSocketNotificationService(ILogger<WebSocketNotificationService> logger)
        {
            _logger = logger;
            _stats = new WebSocketStats
            {
                StartTime = DateTime.UtcNow,
                MessagesSent = 0,
                MessagesFailed = 0
            };

            // Start cleanup timer to remove inactive connections
            _cleanupTimer = new Timer(CleanupInactiveConnections, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Start message queue processor
            Task.Run(ProcessMessageQueue);
        }

        public async Task AddConnectionAsync(string connectionId, WebSocket webSocket, string? userId = null)
        {
            var connection = new WebSocketConnection
            {
                ConnectionId = connectionId,
                WebSocket = webSocket,
                UserId = userId,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                MessagesSent = 0,
                MessagesFailed = 0
            };

            _connections.TryAdd(connectionId, connection);

            if (!string.IsNullOrEmpty(userId))
            {
                _userConnections.AddOrUpdate(userId, 
                    new List<string> { connectionId },
                    (key, existing) => { existing.Add(connectionId); return existing; });
            }

            UpdateStats();
            _logger.LogInformation($"WebSocket connection added: {connectionId} for user: {userId}");
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                if (!string.IsNullOrEmpty(connection.UserId))
                {
                    _userConnections.AddOrUpdate(connection.UserId,
                        new List<string>(),
                        (key, existing) => { existing.Remove(connectionId); return existing; });
                }

                UpdateStats();
                _logger.LogInformation($"WebSocket connection removed: {connectionId} for user: {connection.UserId}");
            }
        }

        public async Task NotifyPaymentUpdateAsync(PaymentUpdateEvent paymentEvent)
        {
            _messageQueue.Enqueue(paymentEvent);
            await ProcessMessageQueue();
        }

        public async Task NotifyUserAsync(string userId, PaymentUpdateEvent paymentEvent)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                var tasks = connectionIds.Select(async connectionId =>
                {
                    if (_connections.TryGetValue(connectionId, out var connection))
                    {
                        await SendToConnectionAsync(connection, paymentEvent);
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogInformation($"Payment update notification sent to user {userId} with {connectionIds.Count} connections");
            }
            else
            {
                _logger.LogWarning($"User {userId} not connected, queuing message");
                _messageQueue.Enqueue(paymentEvent);
            }
        }

        private async Task ProcessMessageQueue()
        {
            if (_isProcessingQueue) return;

            await _queueLock.WaitAsync();
            try
            {
                _isProcessingQueue = true;

                while (_messageQueue.TryDequeue(out var message))
                {
                    await BroadcastToAllConnectionsAsync(message);
                }
            }
            finally
            {
                _isProcessingQueue = false;
                _queueLock.Release();
            }
        }

        private async Task BroadcastToAllConnectionsAsync(PaymentUpdateEvent paymentEvent)
        {
            var message = JsonSerializer.Serialize(paymentEvent);
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);

            var tasks = _connections.Values.Select(async connection =>
            {
                await SendToConnectionAsync(connection, paymentEvent);
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation($"Payment update notification broadcasted to {_connections.Count} connections");
        }

        private async Task SendToConnectionAsync(WebSocketConnection connection, PaymentUpdateEvent paymentEvent)
        {
            try
            {
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    var message = JsonSerializer.Serialize(paymentEvent);
                    var buffer = System.Text.Encoding.UTF8.GetBytes(message);

                    await connection.WebSocket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, buffer.Length),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    connection.LastActivity = DateTime.UtcNow;
                    connection.MessagesSent++;
                    _stats.MessagesSent++;
                }
                else
                {
                    _logger.LogWarning($"Connection {connection.ConnectionId} is not open, state: {connection.WebSocket.State}");
                }
            }
            catch (Exception ex)
            {
                connection.MessagesFailed++;
                _stats.MessagesFailed++;
                _logger.LogError(ex, $"Failed to send WebSocket message to connection {connection.ConnectionId}");
            }
        }

        public async Task<bool> IsUserConnectedAsync(string userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }

        public async Task<int> GetConnectionCountAsync()
        {
            return _connections.Count;
        }

        public async Task<List<string>> GetConnectedUsersAsync()
        {
            return _userConnections.Keys.ToList();
        }

        public async Task<WebSocketStats> GetStatsAsync()
        {
            UpdateStats();
            return _stats;
        }

        private void UpdateStats()
        {
            _stats.TotalConnections = _connections.Count;
            _stats.ActiveConnections = _connections.Values.Count(c => c.WebSocket.State == WebSocketState.Open);
            _stats.UniqueUsers = _userConnections.Count;
            
            _stats.ConnectionsByUser.Clear();
            foreach (var kvp in _userConnections)
            {
                _stats.ConnectionsByUser[kvp.Key] = kvp.Value.Count;
            }
        }

        private void CleanupInactiveConnections(object? state)
        {
            var now = DateTime.UtcNow;
            var inactiveThreshold = TimeSpan.FromMinutes(30);

            var inactiveConnections = _connections.Values
                .Where(c => now - c.LastActivity > inactiveThreshold || c.WebSocket.State != WebSocketState.Open)
                .ToList();

            foreach (var connection in inactiveConnections)
            {
                _logger.LogInformation($"Cleaning up inactive connection: {connection.ConnectionId}");
                RemoveConnectionAsync(connection.ConnectionId).Wait();
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _queueLock?.Dispose();
        }
    }
}

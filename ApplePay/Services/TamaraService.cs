using ApplePay.Models;
using ApplePay.Models.Tamara;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.SqlClient;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApplePay.Services
{
    public sealed class TamaraService
    {
        private readonly HttpClient _http;
        private readonly TamaraOptions _opts;
        private readonly ILogger<TamaraService> _logger;

        private static string TruncateForLog(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length <= max)
                return value;

            return value.Substring(0, max) + "...(truncated)";
        }

        public TamaraService(HttpClient http, IOptions<TamaraOptions> opts, ILogger<TamaraService> logger)
        {
            _http = http;
            _opts = opts.Value;
            _logger = logger;
        }

        public async Task<JsonElement> CreateCheckoutSessionAsync(JsonElement payload, CancellationToken ct)
        {
            return await SendJsonAsync(HttpMethod.Post, "/checkout", payload, ct);
        }

        public async Task<JsonElement> GetOrderAsync(string orderId, CancellationToken ct)
        {
            return await SendAsync(HttpMethod.Get, $"/orders/{orderId}", ct);
        }

        public async Task<JsonElement> AuthoriseOrderAsync(string orderId, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse("{}");
            return await SendJsonAsync(HttpMethod.Post, $"/orders/{orderId}/authorise", doc.RootElement, ct);
        }

        public async Task<JsonElement> CaptureOrderAsync(JsonElement payload, CancellationToken ct)
        {
            return await SendJsonAsync(HttpMethod.Post, "/payments/capture", payload, ct);
        }

        public async Task<JsonElement> SimplifiedRefundAsync(string orderId, JsonElement payload, CancellationToken ct)
        {
            return await SendJsonAsync(HttpMethod.Post, $"/payments/simplified-refund/{orderId}", payload, ct);
        }

        public bool IsDbEnabled()
        {
            return !string.IsNullOrWhiteSpace(_opts.DbConnectionString);
        }

        public async Task EnsureDbInitializedAsync(CancellationToken ct)
        {
            if (!IsDbEnabled())
                return;

            using var conn = new SqlConnection(_opts.DbConnectionString);
            await conn.OpenAsync(ct);

            var createOrders = @"
IF OBJECT_ID('dbo.TamaraOrders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TamaraOrders (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId NVARCHAR(100) NOT NULL,
        OrderReferenceId NVARCHAR(200) NULL,
        Status NVARCHAR(50) NULL,
        Amount DECIMAL(18,2) NULL,
        Currency NVARCHAR(10) NULL,
        RawJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TamaraOrders_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_TamaraOrders_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX IX_TamaraOrders_OrderId ON dbo.TamaraOrders (OrderId);
END
";

            var createEvents = @"
IF OBJECT_ID('dbo.TamaraWebhookEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TamaraWebhookEvents (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId NVARCHAR(100) NOT NULL,
        EventType NVARCHAR(100) NULL,
        Status NVARCHAR(50) NULL,
        RawJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TamaraWebhookEvents_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_TamaraWebhookEvents_OrderId_CreatedAt ON dbo.TamaraWebhookEvents (OrderId, CreatedAt DESC);
END
";

            using (var cmd = new SqlCommand(createOrders, conn))
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }

            using (var cmd = new SqlCommand(createEvents, conn))
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task UpsertOrderAsync(
            string orderId,
            string? orderReferenceId,
            string? status,
            decimal? amount,
            string? currency,
            string rawJson,
            CancellationToken ct)
        {
            if (!IsDbEnabled())
                return;

            await EnsureDbInitializedAsync(ct);

            using var conn = new SqlConnection(_opts.DbConnectionString);
            await conn.OpenAsync(ct);

            var sql = @"
MERGE dbo.TamaraOrders AS target
USING (SELECT @OrderId AS OrderId) AS source
ON (target.OrderId = source.OrderId)
WHEN MATCHED THEN
    UPDATE SET
        OrderReferenceId = @OrderReferenceId,
        Status = @Status,
        Amount = @Amount,
        Currency = @Currency,
        RawJson = @RawJson,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (OrderId, OrderReferenceId, Status, Amount, Currency, RawJson)
    VALUES (@OrderId, @OrderReferenceId, @Status, @Amount, @Currency, @RawJson);
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            cmd.Parameters.AddWithValue("@OrderReferenceId", (object?)orderReferenceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Amount", (object?)amount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Currency", (object?)currency ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RawJson", rawJson);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertWebhookEventAsync(
            string orderId,
            string? eventType,
            string? status,
            string rawJson,
            CancellationToken ct)
        {
            if (!IsDbEnabled())
                return;

            await EnsureDbInitializedAsync(ct);

            using var conn = new SqlConnection(_opts.DbConnectionString);
            await conn.OpenAsync(ct);

            var sql = @"
INSERT INTO dbo.TamaraWebhookEvents (OrderId, EventType, Status, RawJson)
VALUES (@OrderId, @EventType, @Status, @RawJson);
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            cmd.Parameters.AddWithValue("@EventType", (object?)eventType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RawJson", rawJson);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<TamaraOrderRecord?> GetOrderFromDatabaseAsync(string orderId, CancellationToken ct)
        {
            if (!IsDbEnabled())
                return null;

            await EnsureDbInitializedAsync(ct);

            using var conn = new SqlConnection(_opts.DbConnectionString);
            await conn.OpenAsync(ct);

            var sql = @"
SELECT TOP 1
    Id,
    OrderId,
    OrderReferenceId,
    Status,
    Amount,
    Currency,
    RawJson,
    CreatedAt,
    UpdatedAt
FROM dbo.TamaraOrders
WHERE OrderId = @OrderId;
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderId", orderId);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!reader.Read())
                return null;

            return new TamaraOrderRecord
            {
                Id = reader.GetInt32(0),
                OrderId = reader.GetString(1),
                OrderReferenceId = reader.IsDBNull(2) ? null : reader.GetString(2),
                Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                Amount = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                Currency = reader.IsDBNull(5) ? null : reader.GetString(5),
                RawJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = reader.GetDateTime(7),
                UpdatedAt = reader.GetDateTime(8)
            };
        }

        public async Task<List<TamaraWebhookEventRecord>> GetWebhookEventsFromDatabaseAsync(string orderId, int top, CancellationToken ct)
        {
            var results = new List<TamaraWebhookEventRecord>();

            if (!IsDbEnabled())
                return results;

            await EnsureDbInitializedAsync(ct);

            using var conn = new SqlConnection(_opts.DbConnectionString);
            await conn.OpenAsync(ct);

            var sql = @"
SELECT TOP (@Top)
    Id,
    OrderId,
    EventType,
    Status,
    RawJson,
    CreatedAt
FROM dbo.TamaraWebhookEvents
WHERE OrderId = @OrderId
ORDER BY CreatedAt DESC;
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Top", Math.Max(1, Math.Min(200, top)));
            cmd.Parameters.AddWithValue("@OrderId", orderId);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new TamaraWebhookEventRecord
                {
                    Id = reader.GetInt32(0),
                    OrderId = reader.GetString(1),
                    EventType = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                    RawJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }

            return results;
        }

        public static (decimal? amount, string? currency) TryExtractTotalAmount(JsonElement order)
        {
            if (!order.TryGetProperty("total_amount", out var total))
                return (null, null);

            decimal? amount = null;
            if (total.TryGetProperty("amount", out var amt))
            {
                if (amt.ValueKind == JsonValueKind.Number)
                    amount = amt.GetDecimal();
                else if (amt.ValueKind == JsonValueKind.String && decimal.TryParse(amt.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    amount = parsed;
            }

            string? currency = null;
            if (total.TryGetProperty("currency", out var cur))
                currency = cur.GetString();

            return (amount, currency);
        }

        public static string? TryExtractOrderReferenceId(JsonElement order)
        {
            if (order.TryGetProperty("order_reference_id", out var refId))
                return refId.GetString();

            if (order.TryGetProperty("reference_id", out var refId2))
                return refId2.GetString();

            if (order.TryGetProperty("referenceId", out var refId3))
                return refId3.GetString();

            return null;
        }

        private async Task<JsonElement> SendJsonAsync(HttpMethod method, string path, JsonElement payload, CancellationToken ct)
        {
            var url = _http.BaseAddress != null ? new Uri(_http.BaseAddress, path) : new Uri(path, UriKind.RelativeOrAbsolute);
            var requestBody = payload.GetRawText();

            using var req = new HttpRequestMessage(method, path)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            _logger.LogInformation("Tamara request: {Method} {Url}", method.Method, url);

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Tamara response error: {Method} {Url} -> {StatusCode} req={RequestBody} resp={ResponseBody}",
                    method.Method,
                    url,
                    (int)resp.StatusCode,
                    TruncateForLog(requestBody, 4000),
                    TruncateForLog(json, 4000));
                throw new HttpRequestException($"Tamara API {method.Method} {url} {(int)resp.StatusCode}: {json}");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }

        private async Task<JsonElement> SendAsync(HttpMethod method, string path, CancellationToken ct)
        {
            var url = _http.BaseAddress != null ? new Uri(_http.BaseAddress, path) : new Uri(path, UriKind.RelativeOrAbsolute);
            using var req = new HttpRequestMessage(method, path);

            _logger.LogInformation("Tamara request: {Method} {Url}", method.Method, url);

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Tamara response error: {Method} {Url} -> {StatusCode} {Body}",
                    method.Method,
                    url,
                    (int)resp.StatusCode,
                    json);
                throw new HttpRequestException($"Tamara API {method.Method} {url} {(int)resp.StatusCode}: {json}");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }
    }
}

using ApplePay.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.SqlClient;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApplePay.Services
{
    public sealed class UnipalService
    {
        private readonly HttpClient _http;
        private readonly UnipalOptions _opts;
        private readonly ILogger<UnipalService> _logger;

        public UnipalService(HttpClient http, IOptions<UnipalOptions> opts, ILogger<UnipalService> logger)
        {
            _http = http;
            _opts = opts.Value;
            _logger = logger;
            
            _http.BaseAddress = new Uri(_opts.BaseUrl);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
            _http.Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
        }

        public async Task<JsonElement> CreatePaymentAsync(JsonElement payload, CancellationToken ct)
        {
            return await SendJsonAsync(HttpMethod.Post, "/v1/payments", payload, ct);
        }

        public async Task<JsonElement> GetPaymentAsync(string paymentId, CancellationToken ct)
        {
            return await SendAsync(HttpMethod.Get, $"/v1/payments/{paymentId}", ct);
        }

        public async Task<JsonElement> CapturePaymentAsync(string paymentId, JsonElement payload, CancellationToken ct)
        {
            return await SendJsonAsync(HttpMethod.Post, $"/v1/payments/{paymentId}/capture", payload, ct);
        }

        public async Task<JsonElement> RefundPaymentAsync(string paymentId, JsonElement payload, CancellationToken ct)
        {
            return await SendJsonAsync(HttpMethod.Post, $"/v1/payments/{paymentId}/refund", payload, ct);
        }

        public async Task<JsonElement> VoidPaymentAsync(string paymentId, CancellationToken ct)
        {
            return await SendAsync(HttpMethod.Post, $"/v1/payments/{paymentId}/void", ct);
        }

        private async Task<JsonElement> SendAsync(HttpMethod method, string path, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, path);
            
            try
            {
                var response = await _http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(content).RootElement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unipal API request failed: {Method} {Path}", method, path);
                throw;
            }
        }

        private async Task<JsonElement> SendJsonAsync(HttpMethod method, string path, JsonElement payload, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");
            
            try
            {
                var response = await _http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(content).RootElement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unipal API request failed: {Method} {Path} Payload: {Payload}", 
                    method, path, TruncateForLog(payload.GetRawText(), 200));
                throw;
            }
        }

        private static string TruncateForLog(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length <= max)
                return value;

            return value.Substring(0, max) + "...(truncated)";
        }

        // Database operations (if needed)
        public bool IsDbEnabled() => !string.IsNullOrWhiteSpace(_opts.DbConnectionString);

        public async Task InsertWebhookEventAsync(string paymentId, string eventType, string status, string rawBody, CancellationToken ct)
        {
            if (!IsDbEnabled()) return;

            const string sql = @"
                INSERT INTO UnipalWebhookEvents (PaymentId, EventType, Status, RawBody, CreatedAt)
                VALUES (@paymentId, @eventType, @status, @rawBody, @createdAt)";

            try
            {
                await using var conn = new SqlConnection(_opts.DbConnectionString);
                await conn.OpenAsync(ct);
                
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@paymentId", paymentId);
                cmd.Parameters.AddWithValue("@eventType", eventType);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@rawBody", rawBody);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert Unipal webhook event for paymentId={PaymentId}", paymentId);
                throw;
            }
        }

        public async Task UpsertPaymentAsync(string paymentId, string? orderReferenceId, string status, 
            decimal? amount, string? currency, string rawResponse, CancellationToken ct)
        {
            if (!IsDbEnabled()) return;

            const string sql = @"
                MERGE UnipalPayments AS target
                USING (SELECT @paymentId AS PaymentId) AS source
                ON target.PaymentId = source.PaymentId
                WHEN MATCHED THEN
                    UPDATE SET OrderReferenceId = @orderReferenceId, Status = @status, 
                               Amount = @amount, Currency = @currency, RawResponse = @rawResponse, UpdatedAt = @updatedAt
                WHEN NOT MATCHED THEN
                    INSERT (PaymentId, OrderReferenceId, Status, Amount, Currency, RawResponse, CreatedAt, UpdatedAt)
                    VALUES (@paymentId, @orderReferenceId, @status, @amount, @currency, @rawResponse, @createdAt, @updatedAt);";

            try
            {
                await using var conn = new SqlConnection(_opts.DbConnectionString);
                await conn.OpenAsync(ct);
                
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@paymentId", paymentId);
                cmd.Parameters.AddWithValue("@orderReferenceId", (object?)orderReferenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@amount", (object?)amount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@currency", (object?)currency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rawResponse", rawResponse);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
                
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert Unipal payment for paymentId={PaymentId}", paymentId);
                throw;
            }
        }

        public async Task<JsonElement?> GetPaymentFromDatabaseAsync(string paymentId, CancellationToken ct)
        {
            if (!IsDbEnabled()) return null;

            const string sql = "SELECT RawResponse FROM UnipalPayments WHERE PaymentId = @paymentId";

            try
            {
                await using var conn = new SqlConnection(_opts.DbConnectionString);
                await conn.OpenAsync(ct);
                
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@paymentId", paymentId);
                
                var rawResponse = await cmd.ExecuteScalarAsync(ct);
                if (rawResponse == null) return null;
                
                return JsonDocument.Parse(rawResponse.ToString()!).RootElement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Unipal payment from database for paymentId={PaymentId}", paymentId);
                throw;
            }
        }
    }
}

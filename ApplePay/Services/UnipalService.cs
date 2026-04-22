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

        // Database operations using direct SQL
        public bool IsDbEnabled() => !string.IsNullOrWhiteSpace(_opts.DbConnectionString);

        public async Task SavePaymentToDatabaseAsync(
            string paymentId,
            string orderReferenceId,
            string status,
            decimal amount,
            string currency,
            string buyerName,
            string buyerEmail,
            string buyerPhone,
            string rawJson,
            CancellationToken ct)
        {
            string connectionString = !string.IsNullOrWhiteSpace(_opts.DbConnectionString) 
                ? _opts.DbConnectionString 
                : "Server=UTILITIES\\SQLEXPRESS;Database=Unipal;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);

                string query = @"
            INSERT INTO UnipalPayments
            (PaymentId, OrderReferenceId, Status, Amount, Currency, BuyerName, BuyerEmail, BuyerPhone, RawJson)
            VALUES
            (@PaymentId, @OrderReferenceId, @Status, @Amount, @Currency, @BuyerName, @BuyerEmail, @BuyerPhone, @RawJson);
        ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PaymentId", paymentId);
                    cmd.Parameters.AddWithValue("@OrderReferenceId", orderReferenceId);
                    cmd.Parameters.AddWithValue("@Status", status ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Currency", currency ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@BuyerName", buyerName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@BuyerEmail", buyerEmail ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@BuyerPhone", buyerPhone ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RawJson", rawJson);

                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }
        }

        public async Task SaveWebhookEventToDatabaseAsync(
            string paymentId,
            string eventType,
            string status,
            string rawBody,
            CancellationToken ct)
        {
            string connectionString = !string.IsNullOrWhiteSpace(_opts.DbConnectionString) 
                ? _opts.DbConnectionString 
                : "Server=UTILITIES\\SQLEXPRESS;Database=Unipal;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);

                string query = @"
            INSERT INTO UnipalWebhookEvents
            (PaymentId, EventType, Status, RawBody)
            VALUES
            (@PaymentId, @EventType, @Status, @RawBody);
        ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PaymentId", paymentId);
                    cmd.Parameters.AddWithValue("@EventType", eventType);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@RawBody", rawBody);

                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }
        }

        public async Task<JsonElement?> GetPaymentFromDatabaseAsync(string paymentId, CancellationToken ct)
        {
            string connectionString = !string.IsNullOrWhiteSpace(_opts.DbConnectionString) 
                ? _opts.DbConnectionString 
                : "Server=UTILITIES\\SQLEXPRESS;Database=Unipal;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);

                string query = "SELECT RawJson FROM UnipalPayments WHERE PaymentId = @PaymentId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PaymentId", paymentId);

                    var result = await cmd.ExecuteScalarAsync(ct);
                    if (result == null) return null;

                    return JsonDocument.Parse(result.ToString()!).RootElement;
                }
            }
        }
    }
}

using ApplePay.Models;
using ApplePay.Models.Tabby;
using Microsoft.Extensions.Options;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace ApplePay.Services
{
    public sealed class TabbyService
    {
        private readonly HttpClient _http;
        private readonly TabbyOptions _opts;

        public TabbyService(HttpClient http, IOptions<TabbyOptions> opts)
        {
            _http = http;
            _opts = opts.Value;
        }

        public sealed class CreateSessionInput
        {
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "AED";
            public string Description { get; set; } = string.Empty;
            public string BuyerName { get; set; } = string.Empty;
            public string BuyerEmail { get; set; } = string.Empty;
            public string BuyerPhone { get; set; } = string.Empty;
            public string? BuyerDob { get; set; }
            public string OrderReferenceId { get; set; } = string.Empty;
            public string Lang { get; set; } = "en";
            public string? ReturnUrlBase { get; set; }
            public string ShippingCity { get; set; } = string.Empty;
            public string ShippingAddress { get; set; } = string.Empty;
            public string ShippingZip { get; set; } = string.Empty;
            public List<CreateSessionItem>? Items { get; set; }
        }

        public sealed class CreateSessionItem
        {
            public string ReferenceId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public int Quantity { get; set; } = 1;
            public decimal UnitPrice { get; set; }
            public string? ImageUrl { get; set; }
            public string? ProductUrl { get; set; }
            public string Category { get; set; } = "Course";
        }

        public sealed class CreateSessionResult
        {
            public string Status { get; set; } = string.Empty;
            public string? PaymentId { get; set; }
            public string? SessionId { get; set; }
            public string? WebUrl { get; set; }
            public JsonElement Raw { get; set; }
        }

        public async Task<CreateSessionResult> CreateSessionAsync(CreateSessionInput input, CancellationToken ct)
        {
            var items = (input.Items != null && input.Items.Count > 0)
                ? input.Items.Select(i => new
                {
                    reference_id = i.ReferenceId,
                    title = i.Title,
                    quantity = i.Quantity,
                    unit_price = i.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    image_url = i.ImageUrl,
                    product_url = i.ProductUrl,
                    category = i.Category
                }).ToArray()
                : new object[]
                {
                    new
                    {
                        reference_id = string.IsNullOrWhiteSpace(input.OrderReferenceId) ? "ITEM-1" : input.OrderReferenceId,
                        title = string.IsNullOrWhiteSpace(input.Description) ? "Item" : input.Description,
                        quantity = 1,
                        unit_price = input.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                        category = "Course"
                    }
                };

            var payload = new
            {
                payment = new
                {
                    amount = input.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    currency = input.Currency,
                    description = input.Description,
                    buyer = new
                    {
                        name = input.BuyerName,
                        email = input.BuyerEmail,
                        phone = input.BuyerPhone,
                        dob = input.BuyerDob
                    },
                    order = new
                    {
                        reference_id = input.OrderReferenceId,
                        items = items
                    },
                    buyer_history = new
                    {
                        registered_since = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        loyalty_level = 0,
                        wishlist_count = 0,
                        is_social_networks_connected = false,
                        is_phone_number_verified = false,
                        is_email_verified = false
                    },
                    order_history = new object[0]
                },
                lang = input.Lang,
                merchant_code = _opts.MerchantCode,
                merchant_urls = new
                {
                    success = BuildReturnUrl(input.ReturnUrlBase ?? _opts.ReturnUrlBase, _opts.SuccessSuffix),
                    cancel = BuildReturnUrl(input.ReturnUrlBase ?? _opts.ReturnUrlBase, _opts.CancelSuffix),
                    failure = BuildReturnUrl(input.ReturnUrlBase ?? _opts.ReturnUrlBase, _opts.FailureSuffix)
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Tabby Request Payload:\n{jsonPayload}"); // Debug logging
            
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/checkout")
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Tabby API {(int)resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string status = root.GetProperty("status").GetString() ?? string.Empty;
            string? paymentId = root.TryGetProperty("payment", out var p) ? p.GetProperty("id").GetString() : null;
            string? sessionId = root.TryGetProperty("id", out var sid) ? sid.GetString() : null;
            string? webUrl = null;
            if (root.TryGetProperty("configuration", out var cfg)
                && cfg.TryGetProperty("available_products", out var ap)
                && ap.TryGetProperty("installments", out var inst)
                && inst.ValueKind == JsonValueKind.Array && inst.GetArrayLength() > 0)
            {
                var first = inst[0];
                if (first.TryGetProperty("web_url", out var wu))
                {
                    webUrl = wu.GetString();
                }
            }

            return new CreateSessionResult
            {
                Status = status,
                PaymentId = paymentId,
                SessionId = sessionId,
                WebUrl = webUrl,
                Raw = root.Clone()
            };
        }

        public async Task<JsonElement> VerifyPaymentAsync(string paymentId, CancellationToken ct)
        {
            using var resp = await _http.GetAsync($"/api/v2/payments/{paymentId}", ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Tabby API {(int)resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        public async Task<JsonElement> RetrievePaymentAsync(string paymentId, CancellationToken ct)
        {
            using var resp = await _http.GetAsync($"/api/v2/payments/{paymentId}", ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Tabby API {(int)resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract required fields
            string paymentIdVal = root.GetProperty("id").GetString();
            string orderReferenceId = root.GetProperty("order").GetProperty("reference_id").GetString();
            string status = root.GetProperty("status").GetString();
            decimal amount = decimal.Parse(root.GetProperty("amount").GetString());
            string currency = root.GetProperty("currency").GetString();

            string buyerName = root.GetProperty("buyer").GetProperty("name").GetString();
            string buyerEmail = root.GetProperty("buyer").GetProperty("email").GetString();
            string buyerPhone = root.GetProperty("buyer").GetProperty("phone").GetString();

            // Save using SqlCommand
            await SavePaymentToDatabaseAsync(
                paymentIdVal,
                orderReferenceId,
                status,
                amount,
                currency,
                buyerName,
                buyerEmail,
                buyerPhone,
                json,
                ct
            );

            return root.Clone();
        }

        private async Task SavePaymentToDatabaseAsync(
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
            string connectionString =
           "Server=UTILITIES\\SQLEXPRESS;Database=Tabby;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false";


            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);

                string query = @"
            INSERT INTO TabbyPayments
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




        public sealed class CaptureRequest
        {
            public decimal Amount { get; set; }
            public string? ReferenceId { get; set; }
        }

        public async Task<JsonElement> CapturePaymentAsync(string paymentId, CaptureRequest reqModel, CancellationToken ct)
        {
            var payload = new
            {
                amount = reqModel.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                reference_id = reqModel.ReferenceId
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/payments/{paymentId}/captures")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Tabby API {(int)resp.StatusCode}: {json}");
            
            var result = JsonDocument.Parse(json).RootElement.Clone();
            
            // Update payment status in database after successful capture
            await UpdatePaymentStatusInDatabaseAsync(paymentId, "CAPTURED", json, ct);
            
            return result;
        }

        private async Task UpdatePaymentStatusInDatabaseAsync(string paymentId, string status, string rawJson, CancellationToken ct)
        {
            string connectionString =
                "Server=UTILITIES\\SQLEXPRESS;Database=Tabby;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false";

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            string query = @"
                UPDATE TabbyPayments 
                SET Status = @Status, RawJson = @RawJson
                WHERE PaymentId = @PaymentId;
            ";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@PaymentId", paymentId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@RawJson", rawJson);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public sealed class RefundRequest
        {
            public decimal Amount { get; set; }
            public string? Reason { get; set; }
            public string? ReferenceId { get; set; }
        }

        public async Task<JsonElement> RefundPaymentAsync(string paymentId, RefundRequest reqModel, CancellationToken ct)
        {
            var payload = new
            {
                amount = reqModel.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                reason = reqModel.Reason,
                reference_id = reqModel.ReferenceId
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/payments/{paymentId}/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Tabby API {(int)resp.StatusCode}: {json}");
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        private static string BuildReturnUrl(string? baseUrl, string suffix)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return suffix;
            }

            baseUrl = baseUrl.TrimEnd('/') + "/";
            var cleanedSuffix = suffix.TrimStart('/');
            return baseUrl + cleanedSuffix;
        }
        public async Task<TabbyPaymentRecord?> GetPaymentFromDatabaseAsync(
    string paymentId,
    string orderReferenceId,
    CancellationToken ct)
        {
            string connectionString =
                "Server=UTILITIES\\SQLEXPRESS;Database=Tabby;User Id=softsol1_Tap;password=775RAxUz[<B&;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Integrated Security=false";

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            string query = @"
        SELECT TOP 1
            Id,
            PaymentId,
            OrderReferenceId,
            Status,
            Amount,
            Currency,
            BuyerName,
            BuyerEmail,
            BuyerPhone,
            RawJson,
            CreatedAt
        FROM TabbyPayments
        WHERE PaymentId = @PaymentId AND OrderReferenceId = @OrderReferenceId;
    ";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@PaymentId", paymentId);
            cmd.Parameters.AddWithValue("@OrderReferenceId", orderReferenceId);

            using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!reader.Read())
                return null;

            return new TabbyPaymentRecord
            {
                Id = reader.GetInt32(0),
                PaymentId = reader.GetString(1),
                OrderReferenceId = reader.GetString(2),
                Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                Amount = reader.GetDecimal(4),
                Currency = reader.GetString(5),
                BuyerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                BuyerEmail = reader.IsDBNull(7) ? null : reader.GetString(7),
                BuyerPhone = reader.IsDBNull(8) ? null : reader.GetString(8),
                RawJson = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.GetDateTime(10)
            };
        }

    }
}

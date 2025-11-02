using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplePay.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq;

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
                    product_url = i.ProductUrl
                }).ToArray()
                : new object[]
                {
                    new
                    {
                        reference_id = string.IsNullOrWhiteSpace(input.OrderReferenceId) ? "ITEM-1" : input.OrderReferenceId,
                        title = string.IsNullOrWhiteSpace(input.Description) ? "Item" : input.Description,
                        quantity = 1,
                        unit_price = input.Amount.ToString("0.00", CultureInfo.InvariantCulture)
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
                    shipping_address = new
                    {
                        city = input.ShippingCity,
                        address = input.ShippingAddress,
                        zip = input.ShippingZip
                    },
                    order = new
                    {
                        reference_id = input.OrderReferenceId,
                        items = items
                    }
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

            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/checkout")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
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

        public async Task<JsonElement> RetrievePaymentAsync(string paymentId, CancellationToken ct)
        {
            using var resp = await _http.GetAsync($"/api/v2/payments/{paymentId}", ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Tabby API {(int)resp.StatusCode}: {json}");
            return JsonDocument.Parse(json).RootElement.Clone();
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
            return JsonDocument.Parse(json).RootElement.Clone();
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
    }
}

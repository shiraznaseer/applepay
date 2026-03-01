using ApplePay.Models;
using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/tamara")]
    public sealed class TamaraController : ControllerBase
    {
        private readonly TamaraService _tamara;
        private readonly TamaraOptions _opts;
        private readonly ILogger<TamaraController> _logger;

        public TamaraController(TamaraService tamara, IOptions<TamaraOptions> opts, ILogger<TamaraController> logger)
        {
            _tamara = tamara;
            _opts = opts.Value;
            _logger = logger;
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<JsonElement>> CreateCheckout([FromBody] JsonElement payload, CancellationToken ct)
        {
            var normalized = NormalizeCheckoutPayload(payload);

            var adjusted = EnsureNotificationUrl(normalized);

            var missing = ValidateCheckoutPayload(adjusted);
            if (missing.Count > 0)
            {
                return BadRequest(new
                {
                    error = "Invalid Tamara checkout payload",
                    missing
                });
            }
            var res = await _tamara.CreateCheckoutSessionAsync(adjusted, ct);
            return Ok(res);
        }

        [HttpGet("orders/{orderId}")]
        public async Task<ActionResult<JsonElement>> GetOrder([FromRoute] string orderId, CancellationToken ct)
        {
            var res = await _tamara.GetOrderAsync(orderId, ct);
            return Ok(res);
        }

        public sealed class TamaraRefundDto
        {
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "SAR";
            public string? Comment { get; set; }
        }

        [HttpPost("orders/{orderId}/refund")]
        public async Task<ActionResult<JsonElement>> Refund([FromRoute] string orderId, [FromBody] TamaraRefundDto dto, CancellationToken ct)
        {
            var refundPayload = JsonSerializer.SerializeToElement(new
            {
                total_amount = new { amount = dto.Amount, currency = dto.Currency },
                comment = dto.Comment
            });

            var res = await _tamara.SimplifiedRefundAsync(orderId, refundPayload, ct);
            return Ok(res);
        }

        [HttpPost("webhook")]
        public async Task<ActionResult<object>> Webhook([FromBody] JsonElement payload, [FromServices] IWebSocketNotificationService notificationService, CancellationToken ct)
        {
            try
            {
                if (!ValidateNotificationToken(Request, out var principal))
                    return Unauthorized(new { error = "Invalid notification token" });

                var rawBody = payload.GetRawText();
                _logger.LogInformation("Tamara webhook received: {Body}", rawBody);
                LogToFile($"Webhook received: body={rawBody}");

                var eventType = payload.TryGetProperty("event_type", out var et) ? (et.GetString() ?? string.Empty) : string.Empty;
                var orderId = payload.TryGetProperty("order_id", out var oid) ? (oid.GetString() ?? string.Empty) : string.Empty;
                var statusFromPayload = payload.TryGetProperty("status", out var st) ? (st.GetString() ?? string.Empty) : string.Empty;

                if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(orderId))
                    return Ok(new { received = true });

                try
                {
                    await _tamara.InsertWebhookEventAsync(orderId, eventType, statusFromPayload, rawBody, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist Tamara webhook event for orderId={OrderId}", orderId);
                }

                string? orderReferenceId = null;
                string? orderStatus = null;
                decimal amount = 0;
                string? currency = null;

                try
                {
                    var order = await _tamara.GetOrderAsync(orderId, ct);
                    orderReferenceId = TamaraService.TryExtractOrderReferenceId(order);
                    orderStatus = order.TryGetProperty("status", out var os) ? (os.GetString() ?? string.Empty) : string.Empty;

                    var (totalAmount, totalCurrency) = TamaraService.TryExtractTotalAmount(order);
                    if (totalAmount.HasValue)
                        amount = totalAmount.Value;
                    currency = totalCurrency;

                    await _tamara.UpsertOrderAsync(orderId, orderReferenceId, orderStatus, totalAmount, totalCurrency, order.GetRawText(), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch/persist Tamara order snapshot for orderId={OrderId}", orderId);
                }

                if (string.Equals(eventType, "order_approved", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleApprovedAsync(orderId, ct);
                }

                var paymentEvent = new PaymentUpdateEvent
                {
                    Event = "tamara.order.updated",
                    PaymentId = orderId,
                    OrderReferenceId = orderReferenceId ?? string.Empty,
                    Status = string.IsNullOrWhiteSpace(orderStatus) ? eventType : orderStatus,
                    Amount = amount
                };

                try
                {
                    await notificationService.NotifyPaymentUpdateAsync(paymentEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast Tamara webhook update via WebSocket for orderId={OrderId}", orderId);
                }

                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing Tamara webhook");
                return Ok(new { received = true, error = ex.Message });
            }
        }

        [HttpGet("orders/{orderId}/history")]
        public async Task<ActionResult<object>> GetHistory([FromRoute] string orderId, [FromQuery] int top = 50, CancellationToken ct = default)
        {
            if (!_tamara.IsDbEnabled())
                return BadRequest(new { error = "Tamara DB persistence is not enabled" });

            var order = await _tamara.GetOrderFromDatabaseAsync(orderId, ct);
            var events = await _tamara.GetWebhookEventsFromDatabaseAsync(orderId, top, ct);
            return Ok(new { order, events });
        }

        private async Task HandleApprovedAsync(string orderId, CancellationToken ct)
        {
            JsonElement order;
            try
            {
                order = await _tamara.GetOrderAsync(orderId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Tamara order details for orderId={OrderId}", orderId);
                return;
            }

            try
            {
                var status0 = order.TryGetProperty("status", out var s0) ? (s0.GetString() ?? string.Empty) : string.Empty;
                var ref0 = TamaraService.TryExtractOrderReferenceId(order);
                var (amt0, cur0) = TamaraService.TryExtractTotalAmount(order);
                await _tamara.UpsertOrderAsync(orderId, ref0, status0, amt0, cur0, order.GetRawText(), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist Tamara order snapshot before authorise/capture for orderId={OrderId}", orderId);
            }

            var status = order.TryGetProperty("status", out var st) ? (st.GetString() ?? string.Empty) : string.Empty;

            if (string.Equals(status, "authorised", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "fully_captured", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "partially_captured", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(status, "authorised", StringComparison.OrdinalIgnoreCase))
                    return;
            }
            else
            {
                try
                {
                    var authRes = await _tamara.AuthoriseOrderAsync(orderId, ct);
                    _logger.LogInformation("Tamara order authorised: orderId={OrderId} response={Response}", orderId, authRes.GetRawText());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to authorise Tamara orderId={OrderId}", orderId);
                    return;
                }

                try
                {
                    order = await _tamara.GetOrderAsync(orderId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to re-fetch Tamara order after authorise orderId={OrderId}", orderId);
                    return;
                }

                status = order.TryGetProperty("status", out st) ? (st.GetString() ?? string.Empty) : string.Empty;
            }

            if (string.Equals(status, "fully_captured", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "partially_captured", StringComparison.OrdinalIgnoreCase))
                return;

            JsonElement capturePayload;
            try
            {
                capturePayload = BuildCapturePayloadFromOrder(orderId, order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build capture payload from Tamara order details orderId={OrderId}", orderId);
                return;
            }

            try
            {
                var captureRes = await _tamara.CaptureOrderAsync(capturePayload, ct);
                _logger.LogInformation("Tamara order captured: orderId={OrderId} response={Response}", orderId, captureRes.GetRawText());

                try
                {
                    var finalOrder = await _tamara.GetOrderAsync(orderId, ct);
                    var finalStatus = finalOrder.TryGetProperty("status", out var s2) ? (s2.GetString() ?? string.Empty) : string.Empty;
                    var finalRef = TamaraService.TryExtractOrderReferenceId(finalOrder);
                    var (finalAmt, finalCur) = TamaraService.TryExtractTotalAmount(finalOrder);
                    await _tamara.UpsertOrderAsync(orderId, finalRef, finalStatus, finalAmt, finalCur, finalOrder.GetRawText(), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist Tamara order snapshot after capture for orderId={OrderId}", orderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture Tamara orderId={OrderId}", orderId);
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                var logPath = Path.Combine(logDirectory, "tamara-webhook.log");
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch
            {
            }
        }

        private static JsonElement BuildCapturePayloadFromOrder(string orderId, JsonElement order)
        {
            JsonObject orderObj = JsonNode.Parse(order.GetRawText())?.AsObject() ?? new JsonObject();

            var capture = new JsonObject
            {
                ["order_id"] = orderId
            };

            if (orderObj.TryGetPropertyValue("total_amount", out var totalAmount) && totalAmount != null)
                capture["total_amount"] = totalAmount.DeepClone();
            else
                throw new InvalidOperationException("Order is missing total_amount");

            if (orderObj.TryGetPropertyValue("items", out var items) && items != null)
                capture["items"] = items.DeepClone();
            else
                throw new InvalidOperationException("Order is missing items");

            if (orderObj.TryGetPropertyValue("discount_amount", out var discountAmount) && discountAmount != null)
                capture["discount_amount"] = discountAmount.DeepClone();

            if (orderObj.TryGetPropertyValue("shipping_amount", out var shippingAmount) && shippingAmount != null)
                capture["shipping_amount"] = shippingAmount.DeepClone();

            if (orderObj.TryGetPropertyValue("tax_amount", out var taxAmount) && taxAmount != null)
                capture["tax_amount"] = taxAmount.DeepClone();

            return JsonSerializer.SerializeToElement(capture);
        }

        private JsonElement EnsureNotificationUrl(JsonElement payload)
        {
            if (string.IsNullOrWhiteSpace(_opts.NotificationUrl))
                return payload;

            var root = JsonNode.Parse(payload.GetRawText()) as JsonObject;
            if (root == null)
                return payload;

            if (!root.TryGetPropertyValue("merchant_url", out var merchantUrlNode) || merchantUrlNode == null)
            {
                root["merchant_url"] = new JsonObject { ["notification"] = _opts.NotificationUrl };
                return JsonSerializer.SerializeToElement(root);
            }

            var merchantUrl = merchantUrlNode as JsonObject;
            if (merchantUrl == null)
                return payload;

            if (!merchantUrl.TryGetPropertyValue("notification", out var notificationNode) || notificationNode == null || string.IsNullOrWhiteSpace(notificationNode.ToString()))
            {
                merchantUrl["notification"] = _opts.NotificationUrl;
                root["merchant_url"] = merchantUrl;
                return JsonSerializer.SerializeToElement(root);
            }

            return payload;
        }

        private static List<string> ValidateCheckoutPayload(JsonElement payload)
        {
            var missing = new List<string>();

            if (!payload.TryGetProperty("total_amount", out var totalAmount))
            {
                missing.Add("total_amount");
            }
            else
            {
                if (!totalAmount.TryGetProperty("amount", out _))
                    missing.Add("total_amount.amount");
                if (!totalAmount.TryGetProperty("currency", out _))
                    missing.Add("total_amount.currency");
            }

            if (!payload.TryGetProperty("order_reference_id", out _))
                missing.Add("order_reference_id");

            if (!payload.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                missing.Add("items[]");
            }
            else
            {
                var hasAnyItem = false;
                foreach (var item in items.EnumerateArray())
                {
                    hasAnyItem = true;
                    if (!item.TryGetProperty("name", out _))
                        missing.Add("items[].name");
                    if (!item.TryGetProperty("type", out _))
                        missing.Add("items[].type");
                    if (!item.TryGetProperty("quantity", out _))
                        missing.Add("items[].quantity");
                    if (!item.TryGetProperty("unit_price", out _))
                        missing.Add("items[].unit_price");
                    if (!item.TryGetProperty("total_amount", out _))
                        missing.Add("items[].total_amount");
                    break;
                }

                if (!hasAnyItem)
                    missing.Add("items[] (at least 1 item)");
            }

            if (!payload.TryGetProperty("consumer", out var consumer))
            {
                missing.Add("consumer");
            }
            else
            {
                if (!consumer.TryGetProperty("email", out _))
                    missing.Add("consumer.email");
                if (!consumer.TryGetProperty("first_name", out _))
                    missing.Add("consumer.first_name");
                if (!consumer.TryGetProperty("last_name", out _))
                    missing.Add("consumer.last_name");
                if (!consumer.TryGetProperty("phone_number", out _))
                    missing.Add("consumer.phone_number");
            }

            if (!payload.TryGetProperty("country_code", out _))
                missing.Add("country_code");

            if (!payload.TryGetProperty("merchant_url", out var merchantUrl))
            {
                missing.Add("merchant_url");
            }
            else
            {
                if (!merchantUrl.TryGetProperty("success", out _))
                    missing.Add("merchant_url.success");
                if (!merchantUrl.TryGetProperty("cancel", out _))
                    missing.Add("merchant_url.cancel");
                if (!merchantUrl.TryGetProperty("failure", out _))
                    missing.Add("merchant_url.failure");
                if (!merchantUrl.TryGetProperty("notification", out _))
                    missing.Add("merchant_url.notification");
            }

            if (!payload.TryGetProperty("payment_type", out _))
                missing.Add("payment_type");

            return missing.Distinct().ToList();
        }

        private static JsonElement NormalizeCheckoutPayload(JsonElement payload)
        {
            var root = JsonNode.Parse(payload.GetRawText()) as JsonObject;
            if (root == null)
                return payload;

            static void CopyIfMissing(JsonObject obj, string snake, string camel)
            {
                if (!obj.ContainsKey(snake) && obj.TryGetPropertyValue(camel, out var value) && value != null)
                {
                    obj[snake] = value.DeepClone();
                    obj.Remove(camel);
                }
            }

            static void CopyNestedIfMissing(JsonObject obj, string parentSnake, string parentCamel, Action<JsonObject> normalizeChild)
            {
                if (!obj.ContainsKey(parentSnake) && obj.TryGetPropertyValue(parentCamel, out var value) && value is JsonObject child)
                {
                    var clone = (JsonObject)child.DeepClone();
                    normalizeChild(clone);
                    obj[parentSnake] = clone;
                    obj.Remove(parentCamel);
                }
                else if (obj.TryGetPropertyValue(parentSnake, out var existing) && existing is JsonObject existingChild)
                {
                    normalizeChild(existingChild);
                    obj[parentSnake] = existingChild;
                }
            }

            CopyNestedIfMissing(root, "total_amount", "totalAmount", total =>
            {
                CopyIfMissing(total, "amount", "amount");
                CopyIfMissing(total, "currency", "currency");
            });

            CopyIfMissing(root, "order_reference_id", "orderReferenceId");
            CopyIfMissing(root, "order_number", "orderNumber");
            CopyIfMissing(root, "country_code", "countryCode");
            CopyIfMissing(root, "payment_type", "paymentType");
            CopyIfMissing(root, "is_mobile", "isMobile");

            CopyNestedIfMissing(root, "consumer", "consumer", consumer =>
            {
                CopyIfMissing(consumer, "first_name", "firstName");
                CopyIfMissing(consumer, "last_name", "lastName");
                CopyIfMissing(consumer, "phone_number", "phoneNumber");
            });

            CopyNestedIfMissing(root, "merchant_url", "merchantUrl", mu =>
            {
                CopyIfMissing(mu, "success", "success");
                CopyIfMissing(mu, "cancel", "cancel");
                CopyIfMissing(mu, "failure", "failure");
                CopyIfMissing(mu, "notification", "notification");
            });

            if (root.TryGetPropertyValue("items", out var itemsNode) && itemsNode is JsonArray items)
            {
                foreach (var n in items)
                {
                    if (n is not JsonObject item)
                        continue;

                    CopyIfMissing(item, "reference_id", "referenceId");
                    CopyIfMissing(item, "discount_amount", "discountAmount");
                    CopyIfMissing(item, "tax_amount", "taxAmount");
                    CopyIfMissing(item, "unit_price", "unitPrice");
                    CopyIfMissing(item, "total_amount", "totalAmount");
                }

                root["items"] = items;
            }

            var currency = "SAR";
            if (root.TryGetPropertyValue("total_amount", out var taNode) && taNode is JsonObject taObj)
            {
                if (taObj.TryGetPropertyValue("currency", out var curNode) && curNode != null && !string.IsNullOrWhiteSpace(curNode.ToString()))
                    currency = curNode.ToString();
            }

            static void EnsureZeroMoney(JsonObject obj, string key, string currency)
            {
                if (obj.ContainsKey(key))
                    return;
                obj[key] = new JsonObject
                {
                    ["amount"] = 0,
                    ["currency"] = currency
                };
            }

            EnsureZeroMoney(root, "shipping_amount", currency);
            EnsureZeroMoney(root, "tax_amount", currency);

            return JsonSerializer.SerializeToElement(root);
        }

        private bool ValidateNotificationToken(HttpRequest request, out ClaimsPrincipal principal)
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity());

            var token = request.Query["tamaraToken"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(_opts.NotificationToken))
                return false;

            if (string.IsNullOrWhiteSpace(token))
            {
                var auth = request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    token = auth.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
                return false;

            var handler = new JwtSecurityTokenHandler();

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.NotificationToken)),
                ValidateIssuer = !string.IsNullOrWhiteSpace(_opts.NotificationIssuer),
                ValidIssuer = _opts.NotificationIssuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            try
            {
                principal = handler.ValidateToken(token, parameters, out _);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid Tamara notification token");
                return false;
            }
        }
    }
}

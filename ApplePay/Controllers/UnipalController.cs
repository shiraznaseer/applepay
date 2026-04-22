using ApplePay.Models;
using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/unipal")]
    public sealed class UnipalController : ControllerBase
    {
        private readonly UnipalService _unipal;
        private readonly UnipalOptions _opts;
        private readonly ILogger<UnipalController> _logger;

        public UnipalController(UnipalService unipal, IOptions<UnipalOptions> opts, ILogger<UnipalController> logger)
        {
            _unipal = unipal;
            _opts = opts.Value;
            _logger = logger;
        }

        public sealed class CreatePaymentRequest
        {
            [Required]
            public decimal Amount { get; set; }
            
            [Required]
            public string Currency { get; set; } = "USD";
            
            [Required]
            public string OrderReferenceId { get; set; } = string.Empty;
            
            public string Description { get; set; } = string.Empty;
            
            [Required]
            public string ReturnUrl { get; set; } = string.Empty;
            
            public string? CancelUrl { get; set; }
            
            public string? WebhookUrl { get; set; }
            
            public CustomerDto? Customer { get; set; }
            
            public List<ItemDto>? Items { get; set; }
        }

        public sealed class CustomerDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public AddressDto? BillingAddress { get; set; }
            public AddressDto? ShippingAddress { get; set; }
        }

        public sealed class AddressDto
        {
            public string? Line1 { get; set; }
            public string? Line2 { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? PostalCode { get; set; }
            public string? Country { get; set; }
        }

        public sealed class ItemDto
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public int Quantity { get; set; } = 1;
            public decimal UnitPrice { get; set; }
            public string? Category { get; set; }
            public string? ImageUrl { get; set; }
        }

        [HttpPost("payments")]
        public async Task<ActionResult<object>> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken ct)
        {
            try
            {
                var payload = BuildPaymentPayload(request);
                var response = await _unipal.CreatePaymentAsync(payload, ct);
                
                // Extract payment ID and other relevant fields
                var paymentId = response.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var redirectUrl = response.TryGetProperty("redirect_url", out var urlProp) ? urlProp.GetString() : null;
                var status = response.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

                return Ok(new
                {
                    paymentId,
                    status,
                    redirectUrl,
                    raw = response
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to create Unipal payment");
                return StatusCode((int)HttpStatusCode.BadRequest, new { error = ex.Message });
            }
        }

        [HttpGet("payments/{paymentId}")]
        public async Task<ActionResult<JsonElement>> GetPayment([FromRoute] string paymentId, CancellationToken ct)
        {
            try
            {
                var response = await _unipal.GetPaymentAsync(paymentId, ct);
                return Ok(response);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to get Unipal payment: {PaymentId}", paymentId);
                return StatusCode((int)HttpStatusCode.NotFound, new { error = ex.Message });
            }
        }

        public sealed class CaptureRequest
        {
            [Required]
            public decimal Amount { get; set; }
            
            public string? ReferenceId { get; set; }
        }

        [HttpPost("payments/{paymentId}/capture")]
        public async Task<ActionResult<JsonElement>> CapturePayment([FromRoute] string paymentId, [FromBody] CaptureRequest request, CancellationToken ct)
        {
            try
            {
                var payload = JsonSerializer.SerializeToElement(new
                {
                    amount = request.Amount,
                    reference_id = request.ReferenceId
                });
                
                var response = await _unipal.CapturePaymentAsync(paymentId, payload, ct);
                return Ok(response);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to capture Unipal payment: {PaymentId}", paymentId);
                return StatusCode((int)HttpStatusCode.BadRequest, new { error = ex.Message });
            }
        }

        public sealed class RefundRequest
        {
            [Required]
            public decimal Amount { get; set; }
            
            public string? Reason { get; set; }
            
            public string? ReferenceId { get; set; }
        }

        [HttpPost("payments/{paymentId}/refund")]
        public async Task<ActionResult<JsonElement>> RefundPayment([FromRoute] string paymentId, [FromBody] RefundRequest request, CancellationToken ct)
        {
            try
            {
                var payload = JsonSerializer.SerializeToElement(new
                {
                    amount = request.Amount,
                    reason = request.Reason,
                    reference_id = request.ReferenceId
                });
                
                var response = await _unipal.RefundPaymentAsync(paymentId, payload, ct);
                return Ok(response);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to refund Unipal payment: {PaymentId}", paymentId);
                return StatusCode((int)HttpStatusCode.BadRequest, new { error = ex.Message });
            }
        }

        [HttpPost("payments/{paymentId}/void")]
        public async Task<ActionResult<JsonElement>> VoidPayment([FromRoute] string paymentId, CancellationToken ct)
        {
            try
            {
                var response = await _unipal.VoidPaymentAsync(paymentId, ct);
                return Ok(response);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to void Unipal payment: {PaymentId}", paymentId);
                return StatusCode((int)HttpStatusCode.BadRequest, new { error = ex.Message });
            }
        }

        [HttpPost("webhook")]
        public async Task<ActionResult<object>> Webhook([FromBody] JsonElement payload, [FromServices] IWebSocketNotificationService notificationService, CancellationToken ct)
        {
            try
            {
                // Validate webhook signature
                if (!ValidateWebhookSignature(Request))
                {
                    _logger.LogWarning("Invalid Unipal webhook signature received");
                    return Unauthorized(new { error = "Invalid signature" });
                }

                var rawBody = payload.GetRawText();
                _logger.LogInformation("Unipal webhook received: {Body}", rawBody);
                LogToFile($"Webhook received: body={rawBody}");

                // Extract webhook data
                string paymentId = "unknown";
                string orderReferenceId = "unknown";
                string status = "unknown";
                decimal amount = 0;
                string currency = "USD";
                string eventType = "payment.updated";

                if (payload.TryGetProperty("payment", out var paymentElement))
                {
                    paymentId = paymentElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "unknown" : "unknown";
                    status = paymentElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";
                    
                    if (paymentElement.TryGetProperty("amount", out var amountProp))
                    {
                        if (amountProp.ValueKind == JsonValueKind.String && decimal.TryParse(amountProp.GetString(), out var parsedAmount))
                            amount = parsedAmount;
                        else if (amountProp.ValueKind == JsonValueKind.Number)
                            amount = amountProp.GetDecimal();
                    }
                    
                    currency = paymentElement.TryGetProperty("currency", out var currencyProp) ? currencyProp.GetString() ?? "USD" : "USD";
                    
                    if (paymentElement.TryGetProperty("order_reference_id", out var refIdProp))
                        orderReferenceId = refIdProp.GetString() ?? "unknown";
                }

                if (payload.TryGetProperty("event_type", out var eventTypeProp))
                    eventType = eventTypeProp.GetString() ?? "payment.updated";

                _logger.LogInformation("Unipal webhook parsed: paymentId={PaymentId}, orderReferenceId={OrderReferenceId}, status={Status}, amount={Amount}, eventType={EventType}",
                    paymentId, orderReferenceId, status, amount, eventType);

                // Store webhook event
                try
                {
                    await _unipal.SaveWebhookEventToDatabaseAsync(paymentId, eventType, status, rawBody, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist Unipal webhook event for paymentId={PaymentId}", paymentId);
                }

                // Update payment record
                try
                {
                    var payment = await _unipal.GetPaymentAsync(paymentId, ct);
                    
                    // Extract buyer info from payment
                    string buyerName = "", buyerEmail = "", buyerPhone = "";
                    if (payment.TryGetProperty("customer", out var customerProp))
                    {
                        buyerName = customerProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        buyerEmail = customerProp.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
                        buyerPhone = customerProp.TryGetProperty("phone", out var phoneProp) ? phoneProp.GetString() ?? "" : "";
                    }
                    
                    await _unipal.SavePaymentToDatabaseAsync(
                        paymentId, 
                        orderReferenceId, 
                        status, 
                        amount, 
                        currency, 
                        buyerName, 
                        buyerEmail, 
                        buyerPhone, 
                        payment.GetRawText(), 
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch/persist Unipal payment for paymentId={PaymentId}", paymentId);
                }

                // Auto-capture for authorized payments
                if (string.Equals(status, "authorized", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleAuthorizedPaymentAsync(paymentId, amount, orderReferenceId, ct);
                }

                // Send WebSocket notification
                var paymentEvent = new PaymentUpdateEvent
                {
                    Event = $"unipal.{eventType}",
                    PaymentId = paymentId,
                    OrderReferenceId = orderReferenceId ?? string.Empty,
                    Status = status,
                    Amount = amount
                };

                try
                {
                    await notificationService.NotifyPaymentUpdateAsync(paymentEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast Unipal webhook update via WebSocket for paymentId={PaymentId}", paymentId);
                }

                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing Unipal webhook");
                return Ok(new { received = true, error = ex.Message });
            }
        }

        private async Task HandleAuthorizedPaymentAsync(string paymentId, decimal amount, string orderReferenceId, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Auto-capturing authorized Unipal payment: {PaymentId}", paymentId);
                
                var capturePayload = JsonSerializer.SerializeToElement(new
                {
                    amount = amount,
                    reference_id = orderReferenceId
                });
                
                var captureResult = await _unipal.CapturePaymentAsync(paymentId, capturePayload, ct);
                _logger.LogInformation("Unipal payment captured successfully: {PaymentId}", paymentId);
                
                LogToFile($"Auto-capture success: paymentId={paymentId}, result={captureResult.GetRawText()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-capture Unipal payment: {PaymentId}", paymentId);
                LogToFile($"Auto-capture failed: paymentId={paymentId}, error={ex.Message}");
            }
        }

        private JsonElement BuildPaymentPayload(CreatePaymentRequest request)
        {
            var payload = new
            {
                amount = request.Amount,
                currency = request.Currency,
                order_reference_id = request.OrderReferenceId,
                description = request.Description,
                return_url = request.ReturnUrl,
                cancel_url = request.CancelUrl,
                webhook_url = request.WebhookUrl ?? _opts.WebhookUrl,
                merchant_id = _opts.MerchantId,
                customer = request.Customer != null ? new
                {
                    name = request.Customer.Name,
                    email = request.Customer.Email,
                    phone = request.Customer.Phone,
                    billing_address = request.Customer.BillingAddress != null ? new
                    {
                        line1 = request.Customer.BillingAddress.Line1,
                        line2 = request.Customer.BillingAddress.Line2,
                        city = request.Customer.BillingAddress.City,
                        state = request.Customer.BillingAddress.State,
                        postal_code = request.Customer.BillingAddress.PostalCode,
                        country = request.Customer.BillingAddress.Country
                    } : null,
                    shipping_address = request.Customer.ShippingAddress != null ? new
                    {
                        line1 = request.Customer.ShippingAddress.Line1,
                        line2 = request.Customer.ShippingAddress.Line2,
                        city = request.Customer.ShippingAddress.City,
                        state = request.Customer.ShippingAddress.State,
                        postal_code = request.Customer.ShippingAddress.PostalCode,
                        country = request.Customer.ShippingAddress.Country
                    } : null
                } : null,
                items = request.Items?.Select(item => new
                {
                    name = item.Name,
                    description = item.Description,
                    quantity = item.Quantity,
                    unit_price = item.UnitPrice,
                    category = item.Category,
                    image_url = item.ImageUrl
                }).ToList()
            };

            return JsonSerializer.SerializeToElement(payload);
        }

        private bool ValidateWebhookSignature(HttpRequest request)
        {
            if (string.IsNullOrWhiteSpace(_opts.WebhookSecret))
            {
                _logger.LogWarning("Unipal webhook secret not configured, skipping signature validation");
                return true; // Skip validation if not configured
            }

            var signature = request.Headers["X-Unipal-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("No X-Unipal-Signature header found");
                return false;
            }

            // For now, implement basic validation
            // You should implement proper HMAC signature validation here
            // based on Unipal's webhook signature documentation
            var expectedSignature = ComputeSignature(Request.Body, _opts.WebhookSecret);
            return signature == expectedSignature;
        }

        private string ComputeSignature(Stream body, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            
            // Read the body
            using var reader = new StreamReader(body, Encoding.UTF8, leaveOpen: true);
            var bodyText = reader.ReadToEnd();
            body.Position = 0;
            
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(bodyText));
            return Convert.ToBase64String(hash);
        }

        private void LogToFile(string message)
        {
            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                var logPath = Path.Combine(logDirectory, "unipal-webhook.log");
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch
            {
                // Swallow file logging errors to avoid impacting webhook processing
            }
        }
    }
}

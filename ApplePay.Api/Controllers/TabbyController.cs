using ApplePay.Models;
using ApplePay.Models.Tabby;
using ApplePay.Services;
using ApplePay.Tests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.IO;
using System.Text; // system change: file logging support

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/tabby")]
    public sealed class TabbyController : ControllerBase
    {
        private readonly TabbyService _tabby;
        private readonly TabbyOptions _opts;
        private readonly ILogger<TabbyController> _logger; // system change: inject logger for webhook diagnostics

        public TabbyController(TabbyService tabby, IOptions<TabbyOptions> opts, ILogger<TabbyController> logger)
        {
            _tabby = tabby;
            _opts = opts.Value;
            _logger = logger; // system change
        }

        public sealed class CreateSessionRequest
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
            [Required]
            public string? ReturnUrlBase { get; set; }
            public string ShippingCity { get; set; } = string.Empty;
            public string ShippingAddress { get; set; } = string.Empty;
            public string ShippingZip { get; set; } = string.Empty;
            public List<ItemDto>? Items { get; set; }
        }

        public sealed class ItemDto
        {
            public string ReferenceId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public int Quantity { get; set; } = 1;
            public decimal UnitPrice { get; set; }
            public string? ImageUrl { get; set; }
            public string? ProductUrl { get; set; }
            public string Category { get; set; } = "Course";
        }

        [HttpPost("session")]
        public async Task<ActionResult<object>> CreateSession([FromBody] CreateSessionRequest req, CancellationToken ct)
        {
            try
            {
                var res = await _tabby.CreateSessionAsync(new TabbyService.CreateSessionInput
                {
                    Amount = req.Amount,
                    Currency = req.Currency,
                    Description = req.Description,
                    BuyerName = req.BuyerName,
                    BuyerEmail = req.BuyerEmail,
                    BuyerPhone = req.BuyerPhone,
                    BuyerDob = req.BuyerDob,
                    OrderReferenceId = req.OrderReferenceId,
                    Lang = req.Lang,
                    ReturnUrlBase = string.IsNullOrWhiteSpace(req.ReturnUrlBase) ? _opts.ReturnUrlBase : req.ReturnUrlBase,
                    ShippingCity = req.ShippingCity,
                    ShippingAddress = req.ShippingAddress,
                    ShippingZip = req.ShippingZip,
                    Items = req.Items?.Select(i => new TabbyService.CreateSessionItem
                    {
                        ReferenceId = i.ReferenceId,
                        Title = i.Title,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        ImageUrl = i.ImageUrl,
                        ProductUrl = i.ProductUrl,
                        Category = i.Category
                    }).ToList()
                }, ct);
                return Ok(new { status = res.Status, paymentId = res.PaymentId, sessionId = res.SessionId, webUrl = res.WebUrl, raw = res.Raw });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode((int)HttpStatusCode.BadRequest, new { error = ex.Message });
            }
        }

        [HttpGet("payments/{id}")]
        public async Task<ActionResult<JsonElement>> GetPayment([FromRoute] string id, CancellationToken ct)
        {
            var res = await _tabby.RetrievePaymentAsync(id, ct);
            return Ok(res);
        }
        [HttpPost("payments/register")]
        public async Task<ActionResult<JsonElement>> RegisterPayment(
    [FromBody] RegisterPaymentRequest request,
    CancellationToken ct)
        {
            var result = await _tabby.GetPaymentFromDatabaseAsync(request.PaymentId, request.OrderReferenceId, ct);
            return Ok(result);
        }

        public sealed class CaptureDto
        {
            public decimal Amount { get; set; }
            public string? ReferenceId { get; set; }
        }
        [HttpPost("payments/{id}/capture")]
        public async Task<ActionResult<JsonElement>> Capture([FromRoute] string id, [FromBody] CaptureDto dto, CancellationToken ct)
        {
            var res = await _tabby.CapturePaymentAsync(id, new TabbyService.CaptureRequest { Amount = dto.Amount, ReferenceId = dto.ReferenceId }, ct);
            return Ok(res);
        }

        public sealed class RefundDto
        {
            public decimal Amount { get; set; }
            public string? Reason { get; set; }
            public string? ReferenceId { get; set; }
        }
        [HttpPost("payments/{id}/refund")]
        public async Task<ActionResult<JsonElement>> Refund([FromRoute] string id, [FromBody] RefundDto dto, CancellationToken ct)
        {
            var res = await _tabby.RefundPaymentAsync(id, new TabbyService.RefundRequest { Amount = dto.Amount, Reason = dto.Reason, ReferenceId = dto.ReferenceId }, ct);
            return Ok(res);
        }

        [HttpPost("test-webhook")]
        public async Task<ActionResult<object>> TestWebhook([FromServices] IWebSocketNotificationService notificationService, CancellationToken ct)
        {
            try
            {
                var testPayload = JsonDocument.Parse(TabbyWebhookTest.GetTestWebhookJson()).RootElement;
                return await Webhook(testPayload, notificationService, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test webhook");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("webhook")]
        public async Task<ActionResult<object>> Webhook([FromBody] JsonElement payload, [FromServices] IWebSocketNotificationService notificationService, CancellationToken ct)
        {
            try
            {
                var rawBody = payload.GetRawText();
                
                // Validate webhook signature
                if (!ValidateWebhookSignature(Request))
                {
                    _logger.LogWarning("Invalid webhook signature received at {Timestamp}", DateTime.UtcNow);
                    return Unauthorized(new { error = "Invalid signature" });
                }
                
                _logger.LogInformation("Tabby webhook received at {Timestamp} with body: {Body}", DateTime.UtcNow, rawBody); // system change: log full webhook body
                LogToFile($"Webhook received: body={rawBody}"); // system change: persist webhook body to file

                // Extract payment information from webhook payload
                string paymentId = "unknown";
                string orderReferenceId = "unknown";
                string status = "unknown";
                decimal amount = 0;

                try
                {
                    LogToFile("Starting payload parsing...");
                    
                    if (payload.TryGetProperty("payment", out var paymentElement))
                    {
                        LogToFile("Found payment object in payload");
                        paymentId = paymentElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "unknown" : "unknown";
                        status = paymentElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";
                        try
                        {
                            if (payload.TryGetProperty("amount", out var amountProp))
                            {
                                var amountString = amountProp.GetString();
                                LogToFile($"Parsing amount string: {amountString}");
                                amount = decimal.Parse(amountString, CultureInfo.InvariantCulture);
                            }
                        }
                        catch (Exception amountEx)
                        {
                            LogToFile($"Error parsing amount: {amountEx.Message}");
                            amount = 0;
                        }
                        LogToFile($"Parsed from payment object: paymentId={paymentId}, status={status}, amount={amount}");
                    }
                    else if (payload.TryGetProperty("id", out var directIdProp))
                    {
                        LogToFile("No payment object, using direct id property");
                        paymentId = directIdProp.GetString() ?? "unknown";
                        status = payload.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";
                        try
                        {
                            if (payload.TryGetProperty("amount", out var amountProp))
                            {
                                var amountString = amountProp.GetString();
                                LogToFile($"Parsing amount string: {amountString}");
                                amount = decimal.Parse(amountString, CultureInfo.InvariantCulture);
                            }
                        }
                        catch (Exception amountEx)
                        {
                            LogToFile($"Error parsing amount: {amountEx.Message}");
                            amount = 0;
                        }
                        LogToFile($"Parsed from direct properties: paymentId={paymentId}, status={status}, amount={amount}");
                    }
                    else
                    {
                        LogToFile("No payment object or direct id found in payload");
                    }

                    if (payload.TryGetProperty("order", out var orderElement))
                    {
                        orderReferenceId = orderElement.TryGetProperty("reference_id", out var refIdProp) ? refIdProp.GetString() ?? "unknown" : "unknown";
                        LogToFile($"Parsed order reference: {orderReferenceId}");
                    }
                    else
                    {
                        LogToFile("No order object found in payload");
                    }
                }
                catch (Exception parseEx)
                {
                    LogToFile($"Error parsing payload: {parseEx.Message}");
                    _logger.LogError(parseEx, "Error parsing webhook payload");
                }

                _logger.LogInformation("Tabby webhook parsed: paymentId={PaymentId}, orderReferenceId={OrderReferenceId}, status={Status}, amount={Amount}", paymentId, orderReferenceId, status, amount); // system change: log parsed fields
                LogToFile($"Parsed webhook: paymentId={paymentId}, orderReferenceId={orderReferenceId}, status={status}, amount={amount}"); // system change

                try
                {
                    var record = await _tabby.GetPaymentFromDatabaseAsync(paymentId, orderReferenceId, ct); // system change: DB lookup for payment
                    if (record != null)
                    {
                        _logger.LogInformation("Tabby payment record found in database for paymentId={PaymentId}, orderReferenceId={OrderReferenceId}, status={DbStatus}, amount={DbAmount}",
                            record.PaymentId, record.OrderReferenceId, record.Status, record.Amount);
                        LogToFile($"DB record found: paymentId={record.PaymentId}, orderReferenceId={record.OrderReferenceId}, status={record.Status}, amount={record.Amount}"); // system change
                    }
                    else
                    {
                        _logger.LogWarning("No Tabby payment record found in database for paymentId={PaymentId}, orderReferenceId={OrderReferenceId}", paymentId, orderReferenceId);
                        LogToFile($"DB record NOT found: paymentId={paymentId}, orderReferenceId={orderReferenceId}"); // system change
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Error while looking up Tabby payment in database for paymentId={PaymentId}, orderReferenceId={OrderReferenceId}", paymentId, orderReferenceId);
                }

                LogToFile($"AUTHORIZED 1 before{status}"); 
                if (status.ToLower() == "authorized") // system change: auto-capture when authorized
                {
                    LogToFile($"AUTHORIZED 1 after{status}");
                    try
                    {
                        // First verify payment status with Tabby API
                        _logger.LogInformation("Verifying Tabby payment status before capture for paymentId={PaymentId}", paymentId);
                        var paymentVerification = await _tabby.VerifyPaymentAsync(paymentId, ct);
                        string verifiedStatus = paymentVerification.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "unknown" : "unknown";
                        
                        LogToFile($"AUTHORIZED 2 before{verifiedStatus}");
                        if (verifiedStatus.ToLower() == "authorized")
                        {
                            LogToFile($"AUTHORIZED 2 after{verifiedStatus}");
                            _logger.LogInformation("Payment verified as authorized, proceeding with capture for paymentId={PaymentId}, orderReferenceId={OrderReferenceId}, amount={Amount}", paymentId, orderReferenceId, amount);
                            var captureRequest = new TabbyService.CaptureRequest
                            {
                                Amount = amount,
                                ReferenceId = orderReferenceId
                            };

                            var captureResult = await _tabby.CapturePaymentAsync(paymentId, captureRequest, ct); // system change: call capture API
                            _logger.LogInformation("Tabby capture result for paymentId={PaymentId}, orderReferenceId={OrderReferenceId}: {CaptureResult}", paymentId, orderReferenceId, captureResult.ToString());
                            LogToFile($"Capture result: paymentId={paymentId}, orderReferenceId={orderReferenceId}, result={captureResult}"); // system change
                        }
                        else
                        {
                            _logger.LogWarning("Payment verification failed. Expected authorized but got {VerifiedStatus} for paymentId={PaymentId}", verifiedStatus, paymentId);
                            LogToFile($"Verification failed: expected authorized, got {verifiedStatus} for paymentId={paymentId}");
                        }
                    }
                    catch (Exception captureEx)
                    {
                        _logger.LogError(captureEx, "Error while capturing Tabby payment paymentId={PaymentId}, orderReferenceId={OrderReferenceId}", paymentId, orderReferenceId);
                    }
                }

                var paymentEvent = new PaymentUpdateEvent // system change: payment event built from webhook
                {
                    PaymentId = paymentId,
                    OrderReferenceId = orderReferenceId,
                    Status = status,
                    Amount = amount
                };

                _logger.LogInformation("Enqueuing WebSocket payment update notification for paymentId={PaymentId}, orderReferenceId={OrderReferenceId}, status={Status}, amount={Amount}",
                    paymentEvent.PaymentId, paymentEvent.OrderReferenceId, paymentEvent.Status, paymentEvent.Amount); // system change: log WS enqueue
                LogToFile($"WebSocket enqueue: paymentId={paymentEvent.PaymentId}, orderReferenceId={paymentEvent.OrderReferenceId}, status={paymentEvent.Status}, amount={paymentEvent.Amount}"); // system change

                await notificationService.NotifyPaymentUpdateAsync(paymentEvent);

                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing Tabby webhook");
                // Log error but still return success to webhook sender
                return Ok(new { received = true, error = ex.Message });
            }
        }
        
        private bool ValidateWebhookSignature(HttpRequest request)
        {
            // Get the signature from header (you need to configure this in Tabby webhook registration)
            var signature = request.Headers["X-Tabby-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("No X-Tabby-Signature header found");
                return false;
            }
            
            // For now, implement basic validation
            // You should implement proper HMAC signature validation here
            // based on Tabby's signature validation documentation
            var expectedSignature = _opts.SecretKey;
            return signature == expectedSignature; // Replace with proper HMAC validation
        }
        
                private void LogToFile(string message)
        {
            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                var logPath = Path.Combine(logDirectory, "tabby-webhook.log");
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch
            {
                // system change: swallow file logging errors to avoid impacting webhook
            }
        }
    }
}

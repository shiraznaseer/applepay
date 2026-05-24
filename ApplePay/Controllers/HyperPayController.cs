using ApplePay.Models.HyperPay;
using ApplePay.Options;
using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HyperPayController : ControllerBase
    {
        private readonly IHyperPayService _hyperPayService;
        private readonly HyperPayOptions _options;
        private readonly ILogger<HyperPayController> _logger;

        public HyperPayController(
            IHyperPayService hyperPayService,
            IOptions<HyperPayOptions> options,
            ILogger<HyperPayController> logger)
        {
            _hyperPayService = hyperPayService;
            _options = options.Value;
            _logger = logger;
        }

        [HttpGet("applepay/config")]
        public IActionResult GetApplePayConfig()
        {
            try
            {
                var config = new
                {
                    MerchantId = _options.AppleMerchantId,
                    BaseUrl = _options.BaseUrl,
                    EntityId = _options.EntityId,
                    Currency = _options.Currency,
                    IsTestMode = _options.IsTestMode,
                    CurrentDomain = Request.Host.ToString(),
                    RequestScheme = Request.Scheme,
                    FullDomain = $"{Request.Scheme}://{Request.Host}",
                    Timestamp = DateTime.UtcNow
                };

                return Ok(new { success = true, config });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Apple Pay configuration");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Failed to get Apple Pay configuration",
                    error = ex.Message 
                });
            }
        }

        [HttpPost("applepay/checkout")]
        public async Task<IActionResult> CreateApplePayCheckout([FromBody] HyperPayCheckoutRequest request)
        {
            try
            {
                _logger.LogInformation("Creating HyperPay Apple Pay checkout for amount: {Amount} {Currency}", request.Amount, request.Currency);

                var checkout = await _hyperPayService.CreateCheckoutAsync(request);
                var widgetBaseUrl = _options.BaseUrl.TrimEnd('/');

                return Ok(new
                {
                    success = true,
                    checkoutId = checkout.Id,
                    integrity = checkout.Integrity,
                    widgetUrl = $"{widgetBaseUrl}/v1/paymentWidgets.js?checkoutId={checkout.Id}",
                    shopperResultUrl = "https://applepay.tamarran.com/api/hyperpay/return",
                    result = checkout.Result,
                    timestamp = checkout.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HyperPay Apple Pay checkout");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to create HyperPay Apple Pay checkout",
                    error = ex.Message
                });
            }
        }

        [HttpPost("applepay/payment")]
        public async Task<IActionResult> ProcessApplePayPayment([FromBody] ApplePayPaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Processing Apple Pay payment for amount: {Amount} {Currency}", 
                    request.Amount, request.Currency);

                var response = await _hyperPayService.ProcessApplePayPaymentAsync(request);
                
                return Ok(new { 
                    success = true, 
                    paymentId = response.Id,
                    result = response.Result,
                    merchantTransactionId = response.MerchantTransactionId,
                    timestamp = response.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Apple Pay payment");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Failed to process Apple Pay payment",
                    error = ex.Message 
                });
            }
        }

        [HttpGet("payment/{paymentId}/status")]
        public async Task<IActionResult> GetPaymentStatus(string paymentId)
        {
            try
            {
                _logger.LogInformation("Getting payment status for ID: {PaymentId}", paymentId);

                var response = await _hyperPayService.GetPaymentStatusAsync(paymentId);
                
                return Ok(new { 
                    success = true, 
                    payment = response,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment status for ID: {PaymentId}", paymentId);
                return StatusCode(500, new { 
                    success = false, 
                    message = "Failed to get payment status",
                    error = ex.Message 
                });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                _logger.LogInformation("Received HyperPay webhook");

                // Get the raw request body for signature validation
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();
                
                // Get signature from headers
                if (!Request.Headers.TryGetValue("X-Signature", out var signatureValues))
                {
                    _logger.LogWarning("Webhook received without signature");
                    return Unauthorized(new { success = false, message = "Missing signature" });
                }

                var signature = signatureValues.FirstOrDefault();
                
                // TODO: Implement signature validation using your webhook secret
                // For now, we'll proceed with basic validation
                
                // Parse webhook data
                var webhookData = System.Text.Json.JsonSerializer.Deserialize<HyperPayWebhookPayload>(requestBody);
                
                if (webhookData?.Payload == null)
                {
                    _logger.LogWarning("Invalid webhook payload received");
                    return BadRequest(new { success = false, message = "Invalid webhook payload" });
                }

                var payload = webhookData.Payload;
                
                _logger.LogInformation("Processing webhook for payment ID: {PaymentId}, Type: {Type}, Result: {Result}", 
                    payload.Id, payload.Type, payload.Result?.Code);

                // Handle different webhook types
                switch (payload.Type?.ToUpper())
                {
                    case "PAYMENT":
                        await HandlePaymentWebhook(payload);
                        break;
                    case "REGISTRATION":
                        await HandleRegistrationWebhook(payload);
                        break;
                    default:
                        _logger.LogWarning("Unknown webhook type: {Type}", payload.Type);
                        break;
                }

                return Ok(new { success = true, message = "Webhook processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HyperPay webhook");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Failed to process webhook",
                    error = ex.Message 
                });
            }
        }

        private async Task HandlePaymentWebhook(WebhookPayloadData payload)
        {
            // TODO: Update payment status in your database
            // This is where you would:
            // 1. Find the payment by ID
            // 2. Update the status based on result code
            // 3. Send notifications to customer
            // 4. Trigger any business logic

            if (HyperPayResultCodes.IsSuccess(payload.Result?.Code))
            {
                _logger.LogInformation("Payment {PaymentId} completed successfully", payload.Id);
                // Handle successful payment
            }
            else if (HyperPayResultCodes.IsPending(payload.Result?.Code))
            {
                _logger.LogInformation("Payment {PaymentId} is pending", payload.Id);
                // Handle pending payment
            }
            else
            {
                _logger.LogWarning("Payment {PaymentId} failed with result: {Result}", payload.Id, payload.Result?.Code);
                // Handle failed payment
            }
        }

        private async Task HandleRegistrationWebhook(WebhookPayloadData payload)
        {
            // TODO: Handle registration webhooks (for tokenization)
            _logger.LogInformation("Processing registration webhook: {RegistrationId}", payload.Id);
        }

        [AcceptVerbs("GET", "POST")]
        [Route("return")]
        public async Task<IActionResult> HandlePaymentReturn()
        {
            try
            {
                _logger.LogInformation("Received HyperPay payment return");

                string? resourcePath;
                string? checkoutId;

                if (Request.HasFormContentType)
                {
                    var formData = await Request.ReadFormAsync();
                    resourcePath = formData["resourcePath"];
                    checkoutId = formData["id"];
                }
                else
                {
                    resourcePath = Request.Query["resourcePath"];
                    checkoutId = Request.Query["id"];
                }

                if (string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(checkoutId))
                {
                    _logger.LogWarning("Invalid return data received");
                    return BadRequest(new { success = false, message = "Invalid return data" });
                }

                var paymentStatus = await _hyperPayService.GetPaymentStatusAsync(resourcePath);
                
                // Handle different payment results
                if (HyperPayResultCodes.IsSuccess(paymentStatus.Result.Code))
                {
                    return Ok(new { 
                        success = true, 
                        message = "Payment successful",
                        paymentId = paymentStatus.Id,
                        result = paymentStatus.Result
                    });
                }
                else if (HyperPayResultCodes.IsPending(paymentStatus.Result.Code))
                {
                    return Ok(new { 
                        success = false, 
                        message = "Payment is pending",
                        paymentId = paymentStatus.Id,
                        result = paymentStatus.Result
                    });
                }
                else
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Payment failed",
                        paymentId = paymentStatus.Id,
                        result = paymentStatus.Result
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling HyperPay payment return");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Failed to process payment return",
                    error = ex.Message 
                });
            }
        }

        [HttpGet("config")]
        public IActionResult GetConfiguration()
        {
            try
            {
                _logger.LogInformation("Returning HyperPay configuration");

                return Ok(new { 
                    appleMerchantId = _options.AppleMerchantId,
                    isTestMode = _options.IsTestMode,
                    currency = _options.Currency,
                    baseUrl = _options.BaseUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HyperPay configuration");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Failed to get configuration",
                    error = ex.Message 
                });
            }
        }
    }
}

using ApplePay.Interface;
using ApplePay.Models;
using ApplePay.Options;
using ApplePay.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/paymob")]
    public class PaymobController : ControllerBase
    {
        private readonly PaymobService _paymob;
        private readonly IPaymobService _payments;
        private readonly PaymobOptions _options;

        public PaymobController(PaymobService paymob, IPaymobService payments, IOptions<PaymobOptions> options)
        {
            _paymob = paymob;
            _payments = payments;
            _options = options.Value;
        }


        [HttpPost("create-intention")]
        public async Task<ActionResult<JsonElement>> CreateIntention([FromBody] CreateIntentionRequest payload,
            [FromHeader(Name = "X-Paymob-SecretKey"), Required(ErrorMessage = "X-Paymob-SecretKey header is required")] string secretKey,
            [FromHeader(Name = "X-Paymob-PublicKey"), Required(ErrorMessage = "X-Paymob-PublicKey header is required")] string publicKey,CancellationToken ct)
        {
            try
            {
                // Convert payload to JsonElement
                string jsonPayload = JsonSerializer.Serialize(payload);
                var jsonElement = JsonDocument.Parse(jsonPayload).RootElement.Clone();

                var res = await _paymob.CreateIntentionAsync(jsonElement, secretKey, publicKey, ct);
                return Ok(res);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode((int)HttpStatusCode.BadRequest, new { error = ex.Message });
            }
        }
       // [HttpPost("capture")]
       // public async Task<ActionResult<JsonElement>> Capture(
       //[FromBody] CaptureRequest payload,
       //[FromHeader(Name = "X-Paymob-SecretKey"), Required(ErrorMessage = "X-Paymob-SecretKey header is required")] string secretKey,
       //CancellationToken ct)
       // {
       //     try
       //     {
       //         var res = await _paymob.CaptureAsync(payload.TransactionId, payload.AmountCents, secretKey, ct);
       //         return Ok(res);
       //     }
       //     catch (HttpRequestException ex)
       //     {
       //         return StatusCode(400, new { error = ex.Message });
       //     }
       // }
        [HttpPost("void")]
        public async Task<ActionResult<string>> VoidRefund([FromBody] VoidRefundRequest payload,[FromHeader(Name = "X-Paymob-SecretKey"), Required(ErrorMessage = "X-Paymob-SecretKey header is required")] string secretKey,CancellationToken ct)
        {
            try
            {
                var result = await _paymob.VoidRefundAsync(payload.TransactionId, secretKey, ct);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(400, new { error = ex.Message });
            }
        }
        [HttpPost("refund")]
        public async Task<ActionResult<JsonElement>> Refund([FromBody] RefundRequest payload,[FromHeader(Name = "X-Paymob-SecretKey"), Required] string secretKey,CancellationToken ct)
        {
            try
            {
                var res = await _paymob.RefundAsync(payload.TransactionId, payload.AmountCents, secretKey, ct);
                return Ok(res); 
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(400, new { error = ex.Message });
            }
        }

        [HttpPost("callback")]
        public async Task<IActionResult> ProcessedCallback(CancellationToken ct)
        {
            try
            {
                var body = await new System.IO.StreamReader(Request.Body).ReadToEndAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "UNKNOWN";

                if (root.TryGetProperty("obj", out var obj))
                {
                    var success = obj.TryGetProperty("success", out var s) && s.GetBoolean();
                    var transactionId = obj.TryGetProperty("id", out var tid) ? tid.ToString() : "N/A";
                    var amountCents = obj.TryGetProperty("amount_cents", out var ac) ? ac.ToString() : "N/A";
                    var currency = obj.TryGetProperty("currency", out var cur) ? cur.GetString() : "N/A";

                    Console.WriteLine($"[Paymob Callback] type={type} txId={transactionId} success={success} amount={amountCents} {currency}");
                }
                else
                {
                    Console.WriteLine($"[Paymob Callback] type={type} body={body}");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Paymob Callback] Error parsing callback: {ex.Message}");
                return Ok();
            }
        }

        [HttpGet("applepay/config")]
        public IActionResult GetApplePayConfig()
        {
            return Ok(new
            {
                success = true,
                message = "Paymob credentials are supplied through request headers."
            });
        }

        [HttpPost("applepay/create-intention")]
        public async Task<IActionResult> CreateApplePayIntention(
            [FromBody] ApplePayIntentionRequest request,
            [FromHeader(Name = "X-Paymob-SecretKey"), Required(ErrorMessage = "X-Paymob-SecretKey header is required")] string secretKey,
            [FromHeader(Name = "X-Paymob-PublicKey"), Required(ErrorMessage = "X-Paymob-PublicKey header is required")] string publicKey,
            [FromHeader(Name = "X-Paymob-IntegrationId")] string? integrationId,
            [FromHeader(Name = "X-Paymob-ApiKey")] string? apiKey,
            CancellationToken ct)
        {
            try
            {
                int parsedIntegrationId;
                if (!string.IsNullOrWhiteSpace(integrationId))
                {
                    if (!int.TryParse(integrationId, out parsedIntegrationId))
                        return BadRequest(new { success = false, error = "X-Paymob-IntegrationId must be a number." });
                }
                else
                {
                    parsedIntegrationId = _options.CardIntegrationId;
                }

                var payload = new
                {
                    amount = request.Amount,
                    currency = request.Currency,
                    payment_methods = new[] { parsedIntegrationId },
                    items = new[]
                    {
                        new
                        {
                            name = "Test Product",
                            amount = request.Amount,
                            description = "Test payment",
                            quantity = 1
                        }
                    },
                    billing_data = new
                    {
                        apartment = "NA",
                        email = request.Email ?? "test@example.com",
                        floor = "NA",
                        first_name = request.FirstName ?? "Test",
                        street = "NA",
                        building = "NA",
                        phone_number = request.Phone ?? "+966500000000",
                        shipping_method = "NA",
                        postal_code = "NA",
                        city = "Riyadh",
                        country = "SA",
                        last_name = request.LastName ?? "User",
                        state = "Riyadh"
                    },
                    shipping_data = new
                    {
                        apartment = "NA",
                        email = request.Email ?? "test@example.com",
                        floor = "NA",
                        first_name = request.FirstName ?? "Test",
                        street = "NA",
                        building = "NA",
                        phone_number = request.Phone ?? "+966500000000",
                        shipping_method = "NA",
                        postal_code = "NA",
                        city = "Riyadh",
                        country = "SA",
                        last_name = request.LastName ?? "User",
                        state = "Riyadh"
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var jsonElement = JsonDocument.Parse(jsonPayload).RootElement.Clone();

                var res = await _paymob.CreateIntentionAsync(jsonElement, secretKey, publicKey, apiKey, ct);

                return Ok(new
                {
                    success = true,
                    intention = res,
                    redirectUrl = res.GetProperty("redirectUrl").GetString()
                });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}

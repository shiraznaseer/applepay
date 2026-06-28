using ApplePay.Interface;
using ApplePay.Models;
using ApplePay.Options;
using ApplePay.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
        private readonly ILogger<PaymobController> _logger;

        public PaymobController(PaymobService paymob, IPaymobService payments, IOptions<PaymobOptions> options, ILogger<PaymobController> logger)
        {
            _paymob = paymob;
            _payments = payments;
            _options = options.Value;
            _logger = logger;
        }


        [HttpPost("create-intention")]
        public async Task<ActionResult<JsonElement>> CreateIntention([FromBody] CreateIntentionRequest payload,
            [FromHeader(Name = "X-Paymob-SecretKey")] string? secretKey,
            [FromHeader(Name = "X-Paymob-PublicKey")] string? publicKey,CancellationToken ct)
        {
            try
            {
                secretKey = string.IsNullOrWhiteSpace(secretKey) ? _options.SecretKey : secretKey;
                publicKey = string.IsNullOrWhiteSpace(publicKey) ? _options.PublicKey : publicKey;
                if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(publicKey))
                    return BadRequest(new { error = "Paymob SecretKey and PublicKey are required." });

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

                // HMAC verification (Egypt Paymob sends hmac as query parameter)
                var receivedHmac = Request.Query["hmac"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(_options.HmacSecret) && !string.IsNullOrWhiteSpace(receivedHmac))
                {
                    if (root.TryGetProperty("obj", out var objForHmac))
                    {
                        var computedHmac = ComputePaymobHmac(objForHmac, _options.HmacSecret);
                        if (!string.Equals(computedHmac, receivedHmac, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("[Paymob Callback] HMAC mismatch — received={Received}, computed={Computed}", receivedHmac, computedHmac);
                            return Unauthorized(new { error = "HMAC verification failed" });
                        }
                        _logger.LogInformation("[Paymob Callback] HMAC verified OK");
                    }
                }

                var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "UNKNOWN";

                if (root.TryGetProperty("obj", out var obj))
                {
                    var success = obj.TryGetProperty("success", out var s) && s.GetBoolean();
                    var transactionId = obj.TryGetProperty("id", out var tid) ? tid.ToString() : "N/A";
                    var amountCents = obj.TryGetProperty("amount_cents", out var ac) ? ac.ToString() : "N/A";
                    var currency = obj.TryGetProperty("currency", out var cur) ? cur.GetString() : "N/A";

                    _logger.LogInformation("[Paymob Callback] type={Type} txId={TxId} success={Success} amount={Amount} {Currency}",
                        type, transactionId, success, amountCents, currency);
                }
                else
                {
                    _logger.LogInformation("[Paymob Callback] type={Type} body={Body}", type, body);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Paymob Callback] Error parsing callback");
                return Ok();
            }
        }

        private static string ComputePaymobHmac(JsonElement obj, string secret)
        {
            // Paymob Egypt HMAC: concatenate these 20 fields' values in exact order
            static string Val(JsonElement o, string key)
            {
                if (!o.TryGetProperty(key, out var p)) return "";
                return p.ValueKind == JsonValueKind.Null ? "" : p.ToString();
            }

            static string Nested(JsonElement o, string parent, string child)
            {
                if (!o.TryGetProperty(parent, out var p)) return "";
                if (!p.TryGetProperty(child, out var c)) return "";
                return c.ValueKind == JsonValueKind.Null ? "" : c.ToString();
            }

            var sb = new StringBuilder();
            sb.Append(Val(obj, "amount_cents"));
            sb.Append(Val(obj, "created_at"));
            sb.Append(Val(obj, "currency"));
            sb.Append(Val(obj, "error_occured"));
            sb.Append(Val(obj, "has_parent_transaction"));
            sb.Append(Val(obj, "id"));
            sb.Append(Val(obj, "integration_id"));
            sb.Append(Val(obj, "is_3d_secure"));
            sb.Append(Val(obj, "is_auth"));
            sb.Append(Val(obj, "is_capture"));
            sb.Append(Val(obj, "is_refunded"));
            sb.Append(Val(obj, "is_standalone_payment"));
            sb.Append(Val(obj, "is_voided"));
            sb.Append(Nested(obj, "order", "id"));
            sb.Append(Val(obj, "owner"));
            sb.Append(Val(obj, "pending"));
            sb.Append(Nested(obj, "source_data", "pan"));
            sb.Append(Nested(obj, "source_data", "sub_type"));
            sb.Append(Nested(obj, "source_data", "type"));
            sb.Append(Val(obj, "success"));

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var dataBytes = Encoding.UTF8.GetBytes(sb.ToString());
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
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
            [FromHeader(Name = "X-Paymob-SecretKey")] string? secretKey,
            [FromHeader(Name = "X-Paymob-PublicKey")] string? publicKey,
            [FromHeader(Name = "X-Paymob-IntegrationId")] string? integrationId,
            [FromHeader(Name = "X-Paymob-ApiKey")] string? apiKey,
            [FromHeader(Name = "X-Paymob-BaseUrl")] string? baseUrl,
            CancellationToken ct)
        {
            secretKey = string.IsNullOrWhiteSpace(secretKey) ? _options.SecretKey : secretKey;
            publicKey = string.IsNullOrWhiteSpace(publicKey) ? _options.PublicKey : publicKey;
            apiKey = string.IsNullOrWhiteSpace(apiKey) ? _options.ApiKey : apiKey;
            baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? _options.BaseUrl : baseUrl;

            _logger.LogInformation("[Egypt Debug] integrationId header={IntegrationId}, baseUrl header={BaseUrl}, secretKeyPrefix={SecretKeyPrefix}",
                integrationId, baseUrl, secretKey?.Substring(0, Math.Min(20, secretKey?.Length ?? 0)));

            try
            {
                if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(publicKey))
                    return BadRequest(new { success = false, error = "Paymob SecretKey and PublicKey are required." });
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
                        city = request.City ?? (request.Currency == "EGP" ? "Cairo" : "Riyadh"),
                        country = request.Country ?? (request.Currency == "EGP" ? "EG" : "SA"),
                        last_name = request.LastName ?? "User",
                        state = request.State ?? (request.Currency == "EGP" ? "Cairo" : "Riyadh")
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
                        city = request.City ?? (request.Currency == "EGP" ? "Cairo" : "Riyadh"),
                        country = request.Country ?? (request.Currency == "EGP" ? "EG" : "SA"),
                        last_name = request.LastName ?? "User",
                        state = request.State ?? (request.Currency == "EGP" ? "Cairo" : "Riyadh")
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var jsonElement = JsonDocument.Parse(jsonPayload).RootElement.Clone();

                var res = await _paymob.CreateIntentionAsync(jsonElement, secretKey, publicKey, apiKey, baseUrl, ct);

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

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

        public PaymobController(PaymobService paymob, IPaymobService payments)
        {
            _paymob = paymob;
            _payments = payments;
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
    }
}

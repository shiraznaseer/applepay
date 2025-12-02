using ApplePay.Models;
using ApplePay.Models.Tabby;
using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/tabby")]
    public sealed class TabbyController : ControllerBase
    {
        private readonly TabbyService _tabby;
        private readonly TabbyOptions _opts;

        public TabbyController(TabbyService tabby, IOptions<TabbyOptions> opts)
        {
            _tabby = tabby;
            _opts = opts.Value;
        }

        public sealed class CreateSessionRequest
        {
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "SAR";
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
                        ProductUrl = i.ProductUrl
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

        [HttpPost("webhook")]
        public ActionResult<object> Webhook([FromBody] JsonElement payload)
        {
            return Ok(new { received = true });
        }
    }
}

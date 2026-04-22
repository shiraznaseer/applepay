using ApplePay.Models.ZKBio;
using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ApplePay.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QrController : ControllerBase
    {
        private readonly ZKBioService _zkService;
        private readonly ILogger<QrController> _logger;

        public QrController(ZKBioService zkService, ILogger<QrController> logger)
        {
            _zkService = zkService;
            _logger = logger;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] QrRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Pin))
                    return BadRequest(new { success = false, error = "PIN is required" });

                var qr = await _zkService.GetQrDataAsync(request.Pin);

                if (string.IsNullOrEmpty(qr))
                    return BadRequest(new { success = false, error = "Failed to generate QR code" });

                return Ok(new
                {
                    success = true,
                    qrCode = qr,
                    pin = request.Pin
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code for PIN: {Pin}", request?.Pin);
                return BadRequest(new { 
                    success = false, 
                    error = "Failed to generate QR code",
                    details = ex.Message 
                });
            }
        }
    }
}
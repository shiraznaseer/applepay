using ApplePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApplePay.Controllers
{ 
    [ApiController]
    [Route("api")]
    public sealed class PaymentsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CredimaxOptions _options;

        public PaymentsController(IHttpClientFactory httpClientFactory, IOptions<CredimaxOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }
        [HttpPost("payment-process")]
        public async Task<IActionResult> AuthorizePayment([FromBody] ApplePayDTO.ApplePayTokenRequest request)
        {
            string orderId = $"ORD-{Guid.NewGuid():N}".Substring(0, 15);
            string transactionId = $"TXN-{Guid.NewGuid():N}".Substring(0, 15);

            string apiUrl = $"{_options.BaseUrl}/api/rest/version/{_options.ApiVersion}/merchant/{_options.MerchantId}/order/{orderId}/transaction/{transactionId}";

            try
            {
                // --- 1. Build raw ApplePay token EXACTLY as received from Apple ---
                var applePayTokenObject = new
                {
                    version = request.PaymentData.Version,
                    data = request.PaymentData.Data,
                    signature = request.PaymentData.Signature,
                    header = new
                    {
                        ephemeralPublicKey = request.PaymentData.Header.EphemeralPublicKey,
                        publicKeyHash = request.PaymentData.Header.PublicKeyHash,
                        transactionId = request.PaymentData.Header.TransactionId
                    }
                };

                // Serialize this to JSON
                string rawApplePayJson = JsonSerializer.Serialize(
                    applePayTokenObject,
                    new JsonSerializerOptions { PropertyNamingPolicy = null } // Preserve original names
                );

                // --- 3. Build MPGS request payload ---
                var payload = new
                {
                    apiOperation = "PAY",
                    order = new
                    {
                        walletProvider = "APPLE_PAY",
                        amount = request.Price.ToString("F2"),
                        currency = _options.Currency,
                        reference = $"REF-{orderId}"
                    },
                    sourceOfFunds = new
                    {
                        type = "CARD",
                        provided = new
                        {
                            card = new
                            {
                                devicePayment = new
                                {
                                    paymentToken = rawApplePayJson
                                }
                            }
                        }
                    },
                    transaction = new
                    {
                        source = "INTERNET"
                    }
                };

                // Serialize final MPGS payload
                string finalJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                // --- 4. Send HTTP request ---
                var client = _httpClientFactory.CreateClient();
                using var httpRequest = new HttpRequestMessage(HttpMethod.Put, apiUrl);

                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.Username}:{_options.Password}"));
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Content = new StringContent(finalJson, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(httpRequest);
                string responseJson = await response.Content.ReadAsStringAsync();

                return Content(responseJson, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

    }
}
using ApplePay.Models.HyperPay;
using ApplePay.Options;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ApplePay.Services
{
    public interface IHyperPayService
    {
        Task<HyperPayDTO.HyperPayCheckoutResponse> CreateCheckoutAsync(HyperPayCheckoutRequest request);
        Task<HyperPayDTO.HyperPayPaymentResponse> ProcessApplePayPaymentAsync(ApplePayPaymentRequest request);
        Task<HyperPayDTO.HyperPayPaymentResponse> GetPaymentStatusAsync(string paymentId);
    }

    public class HyperPayService : IHyperPayService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HyperPayOptions _options;
        private readonly ILogger<HyperPayService> _logger;

        public HyperPayService(
            IHttpClientFactory httpClientFactory,
            IOptions<HyperPayOptions> options,
            ILogger<HyperPayService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<HyperPayDTO.HyperPayCheckoutResponse> CreateCheckoutAsync(HyperPayCheckoutRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var merchantTransactionId = $"TXN-{Guid.NewGuid():N}".Substring(0, 15);

                var payload = new Dictionary<string, string>
                {
                    ["entityId"] = _options.EntityId,
                    ["amount"] = request.Amount.ToString("F2"),
                    ["currency"] = string.IsNullOrWhiteSpace(request.Currency) ? _options.Currency : request.Currency,
                    ["paymentType"] = string.IsNullOrWhiteSpace(request.PaymentType) ? "DB" : request.PaymentType,
                    ["merchantTransactionId"] = merchantTransactionId,
                    ["customer.email"] = string.IsNullOrWhiteSpace(request.CustomerEmail) ? "test@tamarran.com" : request.CustomerEmail,
                    ["customer.givenName"] = string.IsNullOrWhiteSpace(request.CustomerName) ? "Test" : request.CustomerName,
                    ["customer.surname"] = string.IsNullOrWhiteSpace(request.CustomerSurname) ? "Customer" : request.CustomerSurname,
                    ["billing.street1"] = string.IsNullOrWhiteSpace(request.BillingStreet1) ? "Riyadh" : request.BillingStreet1,
                    ["billing.city"] = string.IsNullOrWhiteSpace(request.BillingCity) ? "Riyadh" : request.BillingCity,
                    ["billing.state"] = string.IsNullOrWhiteSpace(request.BillingState) ? "Riyadh" : request.BillingState,
                    ["billing.country"] = string.IsNullOrWhiteSpace(request.BillingCountry) ? "SA" : request.BillingCountry,
                    ["billing.postcode"] = string.IsNullOrWhiteSpace(request.BillingPostcode) ? "12211" : request.BillingPostcode,
                    ["customParameters[SHOPPER_paypalRiskId]"] = merchantTransactionId
                };

                using var content = new FormUrlEncodedContent(payload);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

                var response = await client.PostAsync($"{_options.BaseUrl}/v1/checkouts", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("HyperPay checkout creation failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    throw new Exception($"HyperPay checkout creation failed: {response.StatusCode} - {responseContent}");
                }

                var checkoutResponse = JsonSerializer.Deserialize<HyperPayDTO.HyperPayCheckoutResponse>(responseContent);

                if (checkoutResponse == null)
                {
                    throw new Exception("Failed to parse HyperPay checkout response");
                }

                return checkoutResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HyperPay checkout");
                throw;
            }
        }

        public async Task<HyperPayDTO.HyperPayPaymentResponse> ProcessApplePayPaymentAsync(ApplePayPaymentRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                
                // Generate unique IDs if not provided
                var orderId = request.OrderId ?? $"ORD-{Guid.NewGuid():N}".Substring(0, 15);
                var merchantTransactionId = $"TXN-{Guid.NewGuid():N}".Substring(0, 15);

                // Build Apple Pay token object
                var applePayTokenObject = new
                {
                    version = request.PaymentToken.Version,
                    data = request.PaymentToken.Data,
                    signature = request.PaymentToken.Signature,
                    header = new
                    {
                        ephemeralPublicKey = request.PaymentToken.Header.EphemeralPublicKey,
                        publicKeyHash = request.PaymentToken.Header.PublicKeyHash,
                        transactionId = request.PaymentToken.Header.TransactionId
                    }
                };

                var rawApplePayJson = JsonSerializer.Serialize(applePayTokenObject, 
                    new JsonSerializerOptions { PropertyNamingPolicy = null });

                // Build HyperPay request payload
                var payload = new
                {
                    entityId = _options.EntityId,
                    amount = request.Amount.ToString("F2"),
                    currency = request.Currency,
                    paymentType = "DB", // Debit booking for immediate payment
                    merchantTransactionId = merchantTransactionId,
                    paymentBrand = "APPLEPAY",
                    customer = new
                    {
                        email = request.CustomerEmail ?? "",
                        givenName = request.CustomerName ?? "",
                        surname = ""
                    },
                    billing = new
                    {
                        street1 = "N/A",
                        city = "N/A",
                        state = "N/A",
                        postcode = "00000",
                        country = "SA"
                    },
                    customParameters = new
                    {
                        APPLEPAY_TOKEN = rawApplePayJson
                    },
                    threeDSecure = new
                    {
                        challengeIndicator = "04" // Challenge requested as per mandate
                    },
                    shopperResultUrl = "https://applepay.tamarran.com/api/hyperpay/return"
                };

                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                
                // Add authentication header
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

                var response = await client.PostAsync($"{_options.BaseUrl}/v1/checkouts", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("HyperPay payment processing failed: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent);
                    throw new Exception($"HyperPay payment processing failed: {response.StatusCode}");
                }

                var paymentResponse = JsonSerializer.Deserialize<HyperPayDTO.HyperPayPaymentResponse>(responseContent);
                
                if (paymentResponse == null)
                {
                    throw new Exception("Failed to parse HyperPay response");
                }

                _logger.LogInformation("HyperPay payment processed successfully. Payment ID: {PaymentId}, Result: {Result}", 
                    paymentResponse.Id, paymentResponse.Result.Code);

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing HyperPay Apple Pay payment");
                throw;
            }
        }

        public async Task<HyperPayDTO.HyperPayPaymentResponse> GetPaymentStatusAsync(string paymentId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var statusPath = paymentId.StartsWith("/", StringComparison.OrdinalIgnoreCase)
                    ? paymentId
                    : $"/v1/checkouts/{paymentId}/payment";

                var separator = statusPath.Contains('?') ? "&" : "?";
                var fullUrl = $"{_options.BaseUrl.TrimEnd('/')}{statusPath}{separator}entityId={_options.EntityId}";

                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

                _logger.LogInformation("Fetching HyperPay payment status from: {Url}", fullUrl);

                var response = await client.GetAsync(fullUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get payment status: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent);
                    throw new Exception($"Failed to get payment status: {response.StatusCode}");
                }

                var paymentResponse = JsonSerializer.Deserialize<HyperPayDTO.HyperPayPaymentResponse>(responseContent);
                
                if (paymentResponse == null)
                {
                    throw new Exception("Failed to parse payment status response");
                }

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment status for ID: {PaymentId}", paymentId);
                throw;
            }
        }
    }
}

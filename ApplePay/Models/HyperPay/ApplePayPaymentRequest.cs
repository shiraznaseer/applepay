using System.Text.Json.Serialization;

namespace ApplePay.Models.HyperPay
{
    public class ApplePayPaymentRequest
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;
        
        [JsonPropertyName("paymentToken")]
        public ApplePayToken PaymentToken { get; set; } = new();
        
        [JsonPropertyName("orderId")]
        public string? OrderId { get; set; }
        
        [JsonPropertyName("customerEmail")]
        public string? CustomerEmail { get; set; }
        
        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }
    }

    public class ApplePayToken
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
        
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
        
        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
        
        [JsonPropertyName("header")]
        public ApplePayTokenHeader Header { get; set; } = new();
    }

    public class ApplePayTokenHeader
    {
        [JsonPropertyName("ephemeralPublicKey")]
        public string EphemeralPublicKey { get; set; } = string.Empty;
        
        [JsonPropertyName("publicKeyHash")]
        public string PublicKeyHash { get; set; } = string.Empty;
        
        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; } = string.Empty;
    }
}

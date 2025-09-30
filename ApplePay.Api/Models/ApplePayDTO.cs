using System.Text.Json.Serialization;

namespace ApplePay.Api.Models
{
    public static class ApplePayDTO
    {
        public sealed class ApplePayTokenRequest
        {
            public decimal Price { get; set; }
            public required ApplePayPaymentData PaymentData { get; set; }
        }

        public sealed class ApplePayPaymentData
        {
            public required string Version { get; set; }
            public required string Data { get; set; }
            public required string Signature { get; set; }
            public required ApplePayPaymentHeader Header { get; set; }
        }

        public sealed class ApplePayPaymentHeader
        {
            public required string EphemeralPublicKey { get; set; }
            public required string PublicKeyHash { get; set; }
            [JsonPropertyName("transactionId")]
            public required string TransactionId { get; set; }
        }
    }
}



using System.Text.Json.Serialization;

namespace ApplePay.Models.HyperPay
{
    public class HyperPayDTO
    {
        public class ApplePaySessionRequest
        {
            [JsonPropertyName("validationUrl")]
            public string ValidationUrl { get; set; } = string.Empty;
            
            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; } = string.Empty;
            
            [JsonPropertyName("domain")]
            public string Domain { get; set; } = string.Empty;
            
            [JsonPropertyName("merchantIdentifier")]
            public string MerchantIdentifier { get; set; } = string.Empty;
        }

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

        public class HyperPayCheckoutResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("integrity")]
            public string? Integrity { get; set; }

            [JsonPropertyName("result")]
            public ResultInfo Result { get; set; } = new();

            [JsonPropertyName("buildNumber")]
            public string BuildNumber { get; set; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public string Timestamp { get; set; } = string.Empty;

            [JsonPropertyName("ndc")]
            public string Ndc { get; set; } = string.Empty;
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
            public TokenHeader Header { get; set; } = new();
        }

        public class TokenHeader
        {
            [JsonPropertyName("ephemeralPublicKey")]
            public string EphemeralPublicKey { get; set; } = string.Empty;
            
            [JsonPropertyName("publicKeyHash")]
            public string PublicKeyHash { get; set; } = string.Empty;
            
            [JsonPropertyName("transactionId")]
            public string TransactionId { get; set; } = string.Empty;
        }

        public class HyperPayPaymentResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            [JsonPropertyName("paymentType")]
            public string PaymentType { get; set; } = string.Empty;
            
            [JsonPropertyName("paymentBrand")]
            public string PaymentBrand { get; set; } = string.Empty;
            
            [JsonPropertyName("amount")]
            public string Amount { get; set; } = string.Empty;
            
            [JsonPropertyName("currency")]
            public string Currency { get; set; } = string.Empty;
            
            [JsonPropertyName("merchantTransactionId")]
            public string MerchantTransactionId { get; set; } = string.Empty;
            
            [JsonPropertyName("result")]
            public ResultInfo Result { get; set; } = new();
            
            [JsonPropertyName("resultDetails")]
            public ResultDetails? ResultDetails { get; set; }
            
            [JsonPropertyName("card")]
            public CardInfo? Card { get; set; }
            
            [JsonPropertyName("threeDSecure")]
            public ThreeDSecureInfo? ThreeDSecure { get; set; }
            
            [JsonPropertyName("buildNumber")]
            public string BuildNumber { get; set; } = string.Empty;
            
            [JsonPropertyName("timestamp")]
            public string Timestamp { get; set; } = string.Empty;
            
            [JsonPropertyName("ndc")]
            public string Ndc { get; set; } = string.Empty;
        }

        public class ResultInfo
        {
            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;
            
            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;
        }

        public class ResultDetails
        {
            [JsonPropertyName("AcquirerResponse")]
            public string AcquirerResponse { get; set; } = string.Empty;
            
            [JsonPropertyName("AcquirerTransactionId")]
            public string AcquirerTransactionId { get; set; } = string.Empty;
            
            [JsonPropertyName("ConnectorTxID3")]
            public string ConnectorTxID3 { get; set; } = string.Empty;
        }

        public class CardInfo
        {
            [JsonPropertyName("bin")]
            public string Bin { get; set; } = string.Empty;
            
            [JsonPropertyName("last4Digits")]
            public string Last4Digits { get; set; } = string.Empty;
            
            [JsonPropertyName("holder")]
            public string Holder { get; set; } = string.Empty;
            
            [JsonPropertyName("expiryMonth")]
            public string ExpiryMonth { get; set; } = string.Empty;
            
            [JsonPropertyName("expiryYear")]
            public string ExpiryYear { get; set; } = string.Empty;
        }

        public class ThreeDSecureInfo
        {
            [JsonPropertyName("verificationId")]
            public string VerificationId { get; set; } = string.Empty;
            
            [JsonPropertyName("eci")]
            public string Eci { get; set; } = string.Empty;
        }
    }
}

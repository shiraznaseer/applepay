using System.Text.Json.Serialization;

namespace ApplePay.Models.HyperPay
{
    public class HyperPayWebhookPayload
    {
        [JsonPropertyName("payload")]
        public WebhookPayloadData Payload { get; set; } = new();
    }

    public class WebhookPayloadData
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
        public WebhookResult Result { get; set; } = new();

        [JsonPropertyName("resultDetails")]
        public WebhookResultDetails? ResultDetails { get; set; }

        [JsonPropertyName("customer")]
        public WebhookCustomer? Customer { get; set; }

        [JsonPropertyName("card")]
        public WebhookCard? Card { get; set; }

        [JsonPropertyName("threeDSecure")]
        public WebhookThreeDSecure? ThreeDSecure { get; set; }

        [JsonPropertyName("buildNumber")]
        public string BuildNumber { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("ndc")]
        public string Ndc { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("registrationId")]
        public string? RegistrationId { get; set; }
    }

    public class WebhookResult
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class WebhookResultDetails
    {
        [JsonPropertyName("AcquirerResponse")]
        public string AcquirerResponse { get; set; } = string.Empty;

        [JsonPropertyName("AcquirerTransactionId")]
        public string AcquirerTransactionId { get; set; } = string.Empty;

        [JsonPropertyName("ConnectorTxID3")]
        public string ConnectorTxID3 { get; set; } = string.Empty;
    }

    public class WebhookCustomer
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("givenName")]
        public string GivenName { get; set; } = string.Empty;

        [JsonPropertyName("surname")]
        public string Surname { get; set; } = string.Empty;
    }

    public class WebhookCard
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

    public class WebhookThreeDSecure
    {
        [JsonPropertyName("verificationId")]
        public string VerificationId { get; set; } = string.Empty;

        [JsonPropertyName("eci")]
        public string Eci { get; set; } = string.Empty;
    }
}

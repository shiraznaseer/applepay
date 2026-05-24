using System.Text.Json.Serialization;

namespace ApplePay.Models.HyperPay
{
    public class HyperPayCheckoutRequest
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "SAR";

        [JsonPropertyName("paymentType")]
        public string PaymentType { get; set; } = "DB";

        [JsonPropertyName("customerEmail")]
        public string? CustomerEmail { get; set; }

        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }

        [JsonPropertyName("customerSurname")]
        public string? CustomerSurname { get; set; }

        [JsonPropertyName("billingStreet1")]
        public string? BillingStreet1 { get; set; }

        [JsonPropertyName("billingCity")]
        public string? BillingCity { get; set; }

        [JsonPropertyName("billingState")]
        public string? BillingState { get; set; }

        [JsonPropertyName("billingCountry")]
        public string? BillingCountry { get; set; }

        [JsonPropertyName("billingPostcode")]
        public string? BillingPostcode { get; set; }
    }
}

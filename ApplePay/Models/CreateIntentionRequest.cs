using System.ComponentModel.DataAnnotations;

namespace ApplePay.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class CreateIntentionRequest
    {
        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("payment_methods")]
        public List<int> PaymentMethods { get; set; } = new();

        [JsonPropertyName("items")]
        public List<Item> Items { get; set; } = new();

        [JsonPropertyName("billing_data")]
        public BillingData BillingData { get; set; } = new();

        [JsonPropertyName("extras")]
        public Dictionary<string, object> Extras { get; set; } = new();

        [JsonPropertyName("special_reference")]
        public string SpecialReference { get; set; } = string.Empty;

        [JsonPropertyName("notification_url")]
        public string NotificationUrl { get; set; } = string.Empty;

        [JsonPropertyName("redirection_url")]
        public string RedirectionUrl { get; set; } = string.Empty;
    }

    public class Item
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }   

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    public class BillingData
    {
        [JsonPropertyName("apartment")]
        public string Apartment { get; set; } = string.Empty;

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("building")]
        public string Building { get; set; } = string.Empty;

        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("floor")]
        public string Floor { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
    }
    public class VoidRefundRequest
    {
        [Required(ErrorMessage = "TransactionId is required")]
        public string TransactionId { get; set; } = string.Empty;
    }
    public class RefundRequest
    {
        [Required(ErrorMessage = "TransactionId is required")]
        public string TransactionId { get; set; } = string.Empty;

        [Required(ErrorMessage = "AmountCents is required")]
        public int AmountCents { get; set; }
    }
    public class CaptureRequest
    {
        [Required(ErrorMessage = "TransactionId is required")]
        public string TransactionId { get; set; } = string.Empty;

        [Required(ErrorMessage = "AmountCents is required")]
        public int AmountCents { get; set; }
    }
    public class TransactionStatusRequest
    {
        [Required(ErrorMessage = "TransactionId is required")]
        public string TransactionId { get; set; } = string.Empty;
    }
}
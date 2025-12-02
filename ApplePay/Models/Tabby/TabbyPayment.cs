using System.Text.Json;

namespace ApplePay.Models.Tabby
{
    public class TabbyPayment
    {
        public int Id { get; set; }
        public string PaymentId { get; set; }
        public string OrderReferenceId { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string BuyerName { get; set; }
        public string BuyerEmail { get; set; }
        public string BuyerPhone { get; set; }
        public string RawJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    public class RegisterPaymentRequest
    {
        public string PaymentId { get; set; }
        public string OrderReferenceId { get; set; }
    }
    public class TabbyPaymentRecord
    {
        public int Id { get; set; }
        public string PaymentId { get; set; }
        public string OrderReferenceId { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string BuyerName { get; set; }
        public string BuyerEmail { get; set; }
        public string BuyerPhone { get; set; }
        public string RawJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public sealed class CreateSessionInput
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "AED";
        public string Description { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public string BuyerPhone { get; set; } = string.Empty;
        public string? BuyerDob { get; set; }
        public string OrderReferenceId { get; set; } = string.Empty;
        public string Lang { get; set; } = "en";
        public string? ReturnUrlBase { get; set; }
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string ShippingZip { get; set; } = string.Empty;
        public List<CreateSessionItem>? Items { get; set; }
    }

    public sealed class CreateSessionItem
    {
        public string ReferenceId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string? ImageUrl { get; set; }
        public string? ProductUrl { get; set; }
    }

    public sealed class CreateSessionResult
    {
        public string Status { get; set; } = string.Empty;
        public string? PaymentId { get; set; }
        public string? SessionId { get; set; }
        public string? WebUrl { get; set; }
        public JsonElement Raw { get; set; }
    }

}

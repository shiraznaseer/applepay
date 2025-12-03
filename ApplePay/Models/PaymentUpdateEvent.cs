namespace ApplePay.Models
{
    public class PaymentUpdateEvent
    {
        public string Event { get; set; } = "payment.updated";
        public string PaymentId { get; set; } = string.Empty;
        public string OrderReferenceId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

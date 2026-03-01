namespace ApplePay.Models.Tamara
{
    public sealed class TamaraOrderRecord
    {
        public int Id { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string? OrderReferenceId { get; set; }
        public string? Status { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string? RawJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class TamaraWebhookEventRecord
    {
        public int Id { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string? EventType { get; set; }
        public string? Status { get; set; }
        public string? RawJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace ApplePay.Models.Unipal
{
    public sealed class UnipalPayment
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string PaymentId { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? OrderReferenceId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;
        
        public decimal? Amount { get; set; }
        
        [MaxLength(10)]
        public string? Currency { get; set; }
        
        public string? RawResponse { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class UnipalWebhookEvent
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string PaymentId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;
        
        public string? RawBody { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

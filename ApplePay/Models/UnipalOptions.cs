namespace ApplePay.Models
{
    public sealed class UnipalOptions
    {
        public const string SectionName = "Unipal";

        public string BaseUrl { get; set; } = "https://api.unipal.com";
        public required string ApiKey { get; set; }
        public required string SecretKey { get; set; }
        public required string MerchantId { get; set; }
        
        public string? WebhookSecret { get; set; }
        public string? WebhookUrl { get; set; }
        public string? ReturnUrlBase { get; set; }
        public string? DbConnectionString { get; set; }
        
        // Timeout settings
        public int TimeoutSeconds { get; set; } = 30;
        
        // Retry settings
        public int MaxRetries { get; set; } = 3;
    }
}

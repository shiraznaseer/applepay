namespace ApplePay.Models
{
    public sealed class TamaraOptions
    {
        public const string SectionName = "Tamara";

        public string BaseUrl { get; set; } = "https://api-sandbox.tamara.co";
        public required string ApiToken { get; set; }
        public required string NotificationToken { get; set; }
        public string NotificationIssuer { get; set; } = "Tamara";
        public string? NotificationUrl { get; set; }
        public string? DbConnectionString { get; set; }
    }
}

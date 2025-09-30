namespace ApplePay.Models
{
    public sealed class CredimaxOptions
    {
        public const string SectionName = "Credimax";

        public required string MerchantId { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public string Currency { get; set; } = "BHD";
        public int ApiVersion { get; set; } = 71;
        public string BaseUrl { get; set; } = "https://credimax.gateway.mastercard.com";
    }
}

namespace ApplePay.Options
{
    public sealed class PaymobOptions
    {
        public const string SectionName = "Paymob";

        public string? BaseUrl { get; set; }
        public string? IntentionPath { get; set; }
        public string? VoidUrl { get; set; }
        public string? RefundUrl { get; set; }
        public string? CaptureUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? SecretKey { get; set; }
        public string? PublicKey { get; set; }
        public int CardIntegrationId { get; set; }
        public string? HmacSecret { get; set; }
    }
}

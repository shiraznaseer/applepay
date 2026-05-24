namespace ApplePay.Options
{
    public sealed class HyperPayOptions
    {
        public const string SectionName = "HyperPay";

        public string BaseUrl { get; set; } = "https://test.oppwa.com";
        public string EntityId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string AppleMerchantId { get; set; } = string.Empty;
        public string Currency { get; set; } = "SAR";
        public bool IsTestMode { get; set; } = true;
    }
}

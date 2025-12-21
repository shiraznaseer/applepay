namespace ApplePay.Models
{
    public sealed class TabbyOptions
    {
        public const string SectionName = "Tabby";

        public string BaseUrl { get; set; } = "https://api.tabby.ai";
        public required string SecretKey { get; set; }
        public required string PublicKey { get; set; }
        public required string MerchantCode { get; set; }

        public required string ReturnUrlBase { get; set; }
        public string SuccessSuffix { get; set; } = "tabby-successs";
        public string CancelSuffix { get; set; } = "tabby-cancel";
        public string FailureSuffix { get; set; } = "tabby-failure";
        public string TabbywebhookURL { get; set; }
    }
}

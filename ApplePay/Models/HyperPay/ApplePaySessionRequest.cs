using System.Text.Json.Serialization;

namespace ApplePay.Models.HyperPay
{
    public class ApplePaySessionRequest
    {
        [JsonPropertyName("validationUrl")]
        public string ValidationUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
        
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;
        
        [JsonPropertyName("merchantIdentifier")]
        public string MerchantIdentifier { get; set; } = string.Empty;
    }
}

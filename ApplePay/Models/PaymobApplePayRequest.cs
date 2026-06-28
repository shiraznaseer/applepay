using System.ComponentModel.DataAnnotations;

namespace ApplePay.Models
{
    public class ApplePayIntentionRequest
    {
        [Required(ErrorMessage = "Amount is required")]
        public int Amount { get; set; }

        [Required(ErrorMessage = "Currency is required")]
        public string Currency { get; set; } = "SAR";

        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
    }
}

using System.Text.Json;

namespace ApplePay.Models
{
    public class ApplePayPaymentRequest
    {
        public string apiOperation { get; set; }
        public Order order { get; set; }
        public SourceOfFunds sourceOfFunds { get; set; }
        public Device device { get; set; }
        public Transaction transaction { get; set; }
    }

    public class SourceOfFunds
    {
        public string type { get; set; }
        public Provided provided { get; set; }
    }

    public class Provided
    {
        public Card card { get; set; }
    }

    public class DevicePayment
    {
        public string paymentToken { get; set; }
    }

    public class Device
    {
        public string ani { get; set; }
    }

}

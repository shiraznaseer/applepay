namespace ApplePay.Models
{
    public class PaymentResponsecs
    {
        public Device device { get; set; }
        public string gatewayEntryPoint { get; set; }
        public string merchant { get; set; }
        public Order order { get; set; }
        public Response response { get; set; }
        public string result { get; set; }
        public Risk risk { get; set; }
        public SourceOfFunds sourceOfFunds { get; set; }
        public DateTime timeOfLastUpdate { get; set; }
        public DateTime timeOfRecord { get; set; }
        public Transaction transaction { get; set; }
        public string version { get; set; }
    }
    public class Acquirer
    {
        public string id { get; set; }
        public string merchantId { get; set; }
    }

    public class Card
    {
        public string brand { get; set; }
        public DevicePayment devicePayment { get; set; }
        public DeviceSpecificExpiry deviceSpecificExpiry { get; set; }
        public string deviceSpecificNumber { get; set; }
        public Expiry expiry { get; set; }
        public string fundingMethod { get; set; }
        public string number { get; set; }
        public string scheme { get; set; }
        public string storedOnFile { get; set; }
    }

    public class Chargeback
    {
        public int amount { get; set; }
        public string currency { get; set; }
    }

    public class DeviceSpecificExpiry
    {
        public string month { get; set; }
        public string year { get; set; }
    }

    public class Expiry
    {
        public string month { get; set; }
        public string year { get; set; }
    }

    public class Order
    {
        public double amount { get; set; }
        public string authenticationStatus { get; set; }
        public Chargeback chargeback { get; set; }
        public DateTime creationTime { get; set; }
        public string currency { get; set; }
        public string id { get; set; }
        public DateTime lastUpdatedTime { get; set; }
        public double merchantAmount { get; set; }
        public string merchantCategoryCode { get; set; }
        public string merchantCurrency { get; set; }
        public string status { get; set; }
        public double totalAuthorizedAmount { get; set; }
        public double totalCapturedAmount { get; set; }
        public double totalDisbursedAmount { get; set; }
        public double totalRefundedAmount { get; set; }
        public string walletProvider { get; set; }
    }

    public class Response
    {
        public string gatewayCode { get; set; }
        public string gatewayRecommendation { get; set; }
        public string provider { get; set; }
        public Review review { get; set; }
        public List<Rule> rule { get; set; }
        public int totalScore { get; set; }
    }

    public class Review
    {
        public string decision { get; set; }
    }

    public class Risk
    {
        public Response response { get; set; }
    }
    public class Rule
    {
        public string id { get; set; }
        public string name { get; set; }
        public int score { get; set; }
        public string type { get; set; }
        public string data { get; set; }
        public string recommendation { get; set; }
    }


    public class Transaction
    {
        public Acquirer acquirer { get; set; }
        public double amount { get; set; }
        public string authenticationStatus { get; set; }
        public string currency { get; set; }
        public string id { get; set; }
        public string source { get; set; }
        public string stan { get; set; }
        public string type { get; set; }
    }
}

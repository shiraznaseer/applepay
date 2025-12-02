using System.Collections.Concurrent;

namespace ApplePay.Interface
{
    public interface IPaymobService
    {
        void SaveStatus(string transactionId, string status, string rawJson);
    }

    public sealed class InMemoryPaymobPaymentRepository : IPaymobService
    {
        private readonly ConcurrentDictionary<string, (string Status, string RawJson)> _store = new();

        public void SaveStatus(string transactionId, string status, string rawJson)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                transactionId = Guid.NewGuid().ToString("N");
            }

            _store[transactionId] = (status, rawJson);
        }
    }
}

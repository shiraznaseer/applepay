using ApplePay.Controllers;
using ApplePay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApplePay.Tests
{
    public class TabbyWebhookTest
    {
        public static object CreateTestWebhookPayload()
        {
            return new
            {
                id = "test_payment_" + Guid.NewGuid().ToString("N")[..8],
                status = "AUTHORIZED",
                amount = "100.00",
                currency = "AED",
                payment = new
                {
                    id = "test_payment_" + Guid.NewGuid().ToString("N")[..8],
                    status = "AUTHORIZED",
                    amount = "100.00",
                    currency = "AED",
                    buyer = new
                    {
                        name = "Test User",
                        email = "test@example.com",
                        phone = "+971501234567"
                    }
                },
                order = new
                {
                    reference_id = "TEST_ORDER_" + DateTime.UtcNow.Ticks
                },
                created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        public static string GetTestWebhookJson()
        {
            var payload = CreateTestWebhookPayload();
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

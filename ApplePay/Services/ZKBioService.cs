using ApplePay.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ApplePay.Services
{
    public class ZKBioService
    {
        private readonly ZKBioOptions _options;
        private readonly HttpClient _client;
        private string _authToken;

        public ZKBioService(IOptions<ZKBioOptions> options)
        {
            _options = options.Value;

            _client = new HttpClient()
            {
                BaseAddress = new Uri(_options.BaseUrl)
            };
        }

        // ✅ Get QR string from API
        public async Task<string> GetQrDataAsync(string pin)
        {
            if (string.IsNullOrEmpty(pin))
                throw new ArgumentException("PIN is required");

            var url = $"/api/person/getQrCode/{pin}?access_token={_options.ClientSecret}";

            var body = new
            {
                pin = pin
            };

            var content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(body),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _client.PostAsync(url, content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"API Response: {result}");

            // ❌ HTML means wrong URL/token
            if (result.TrimStart().StartsWith("<"))
                throw new Exception("API returned HTML → check BaseUrl or Token");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP Error: {response.StatusCode}");

            var json = JObject.Parse(result);

            if ((int)json["code"] != 0)
                throw new Exception($"API Error: {json["message"]}");

            return json["data"]?.ToString();
        }

        // MD5 Helper (IMPORTANT)
        private string GetMd5(string input)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}
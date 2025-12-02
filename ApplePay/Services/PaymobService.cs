using ApplePay.Interface;
using ApplePay.Models;
using ApplePay.Options;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ApplePay.Services
{
    public sealed class PaymobService
    {
        private readonly HttpClient _http;
        private readonly PaymobOptions _opts;
        private readonly IPaymobService _payments;

        public PaymobService(HttpClient http, IOptions<PaymobOptions> opts, IPaymobService payments)
        {
            _http = http;
            _opts = opts.Value;
            _payments = payments;
        }

        public async Task<JsonElement> CreateIntentionAsync(JsonElement payload, string secretKey, string publicKey, CancellationToken ct)
        {
            var path = string.IsNullOrWhiteSpace(_opts.IntentionPath)
           ? "/v1/intention/"
           : (_opts.IntentionPath!.StartsWith("/") ? _opts.IntentionPath : "/" + _opts.IntentionPath);

            using var req = new HttpRequestMessage(HttpMethod.Post, _opts.BaseUrl + path)
            {
                Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(secretKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Token", secretKey);

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Paymob Intention API {(int)resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Build redirect URL
            string clientSecret = root.GetProperty("client_secret").GetString()!;
            string redirectUrl = $"https://accept.paymob.com/unifiedcheckout/?publicKey={publicKey}&clientSecret={clientSecret}";

            var dictionary = new Dictionary<string, object>();
            foreach (var prop in root.EnumerateObject())
                dictionary[prop.Name] = prop.Value.Clone();

            dictionary["redirectUrl"] = redirectUrl;

            string modifiedJson = JsonSerializer.Serialize(dictionary);
            return JsonDocument.Parse(modifiedJson).RootElement.Clone();
        }
        public async Task<JsonElement> VoidRefundAsync(string transactionId, string secretKey, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _opts.VoidUrl);
            request.Headers.Add("Authorization", $"Token {secretKey}");

            var payload = new { transaction_id = transactionId };
            string jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            var responseString = await response.Content.ReadAsStringAsync(ct);

            // Optional: throw exception if not success
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Paymob Void Refund API {(int)response.StatusCode}: {responseString}");

            // Convert response to JsonElement
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.Clone();
        }

        private sealed record CanonicalResult(bool Success, string DataString, string? Error);

        private static CanonicalResult BuildCanonicalString(JsonElement payload)
        {
            var obj = payload.EnumerateObject()
                .Where(p => !string.Equals(p.Name, "hmac", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

            if (obj.Length == 0)
            {
                return new CanonicalResult(false, string.Empty, "No fields to sign");
            }

            var sb = new StringBuilder();
            foreach (var prop in obj)
            {
                sb.Append(prop.Name);
                sb.Append('=');
                sb.Append(prop.Value.ToString());
            }

            return new CanonicalResult(true, sb.ToString(), null);
        }

        private static string ComputeHmacSha512Hex(string data, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var dataBytes = Encoding.UTF8.GetBytes(data);

            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
        public async Task<JsonElement> RefundAsync(string transactionId, int amountCents, string secretKey, CancellationToken ct)
        {

            using var request = new HttpRequestMessage(HttpMethod.Post, _opts.RefundUrl);
            request.Headers.Add("Authorization", $"Token {secretKey}");

            // Build JSON payload
            var payload = new
            {
                transaction_id = transactionId,
                amount_cents = amountCents
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Send request
            using var response = await _http.SendAsync(request, ct);
            var responseString = await response.Content.ReadAsStringAsync(ct);

            // Throw if API returns non-success
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Paymob Refund API {(int)response.StatusCode}: {responseString}");

            // Parse the response JSON and return as JsonElement
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.Clone(); // clone to avoid disposal issues
        }
        //public async Task<JsonElement> CaptureAsync(string transactionId, int amountCents, string secretKey, CancellationToken ct)
        //{
        //    using var request = new HttpRequestMessage(HttpMethod.Post, _opts.CaptureUrl);
        //    request.Headers.Add("Authorization", $"Token {secretKey}");

        //    var payload = new
        //    {
        //        transaction_id = transactionId,
        //        amount_cents = amountCents
        //    };

        //    string jsonPayload = JsonSerializer.Serialize(payload);
        //    request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        //    using var response = await _http.SendAsync(request, ct);
        //    var responseString = await response.Content.ReadAsStringAsync(ct);

        //    if (!response.IsSuccessStatusCode)
        //        throw new HttpRequestException($"Paymob Capture API {(int)response.StatusCode}: {responseString}");

        //    using var doc = JsonDocument.Parse(responseString);
        //    return doc.RootElement.Clone();
        //}
        //public async Task<bool> IsAuthorizedAsync(string transactionId, string secretKey, CancellationToken ct)
        //{
        //    var url = $"https://accept.paymob.com/api/acceptance/transactions/{transactionId}";

        //    using var request = new HttpRequestMessage(HttpMethod.Get, url);
        //    request.Headers.Add("Authorization", $"Token {secretKey}");

        //    using var response = await _http.SendAsync(request, ct);
        //    var json = await response.Content.ReadAsStringAsync(ct);

        //    if (!response.IsSuccessStatusCode)
        //        throw new HttpRequestException($"Paymob Transaction API {(int)response.StatusCode}: {json}");

        //    using var doc = JsonDocument.Parse(json);
        //    var status = doc.RootElement.GetProperty("status").GetString();

        //    return status == "AUTH"; // true if authorized
        //}
    }
    }

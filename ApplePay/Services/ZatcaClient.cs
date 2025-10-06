using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApplePay.Api.Options;

namespace ApplePay.Api.Services;

public sealed class ZatcaClient
{
    private readonly HttpClient _http;
    private readonly bool _useSandboxHeaders;

    public ZatcaClient(HttpClient http, Microsoft.Extensions.Options.IOptions<ZatcaOptions> options)
    {
        _http = http;
        _useSandboxHeaders = options.Value.UseSandboxHeaders;
    }

    public sealed class ComplianceResponse
    {
        public string RequestId { get; }
        public string Secret { get; }
        public string BinarySecurityToken { get; }
        public JsonElement RawJson { get; }
        public ComplianceResponse(string requestId, string secret, string binarySecurityToken, JsonElement rawJson)
        {
            RequestId = requestId;
            Secret = secret;
            BinarySecurityToken = binarySecurityToken;
            RawJson = rawJson;
        }
    }

    public sealed class ProductionCsidResponse
    {
        public string BinarySecurityToken { get; }
        public string Secret { get; }
        public JsonElement RawJson { get; }
        public ProductionCsidResponse(string token, string secret, JsonElement raw)
        {
            BinarySecurityToken = token;
            Secret = secret;
            RawJson = raw;
        }
    }

    private static string EnsurePemWrappedBase64(string csrBase64)
    {
        if (!string.IsNullOrEmpty(csrBase64) && csrBase64.StartsWith("LS0tLS1"))
            return csrBase64;

        static string Chunk(string s, int size)
        {
            var sb = new StringBuilder(s.Length + s.Length / size + 10);
            for (int i = 0; i < s.Length; i += size)
                sb.Append(s, i, Math.Min(size, s.Length - i)).Append('\n');
            return sb.ToString();
        }
        var body = Chunk(csrBase64, 64);
        var pem = $"-----BEGIN CERTIFICATE REQUEST-----\n{body}-----END CERTIFICATE REQUEST-----\n";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));
    }

    public async Task<ComplianceResponse> RequestComplianceAsync(string csrBase64, string otp, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/e-invoicing/developer-portal/compliance");
        msg.Headers.TryAddWithoutValidation("accept", "application/json");
        msg.Headers.TryAddWithoutValidation("Accept-Version", "V2");
        msg.Headers.TryAddWithoutValidation("accept-language", "en");
        msg.Headers.TryAddWithoutValidation("OTP", otp);

        // Accept Base64-of-PEM or raw DER Base64
        string csrForApi = csrBase64.StartsWith("LS0tLS1") ? csrBase64 : EnsurePemWrappedBase64(csrBase64);
        var payload = new { csr = csrForApi };
        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var xid = resp.Headers.TryGetValues("X-Global-Transaction-ID", out var vals) ? string.Join(",", vals) : string.Empty;
            throw new InvalidOperationException($"Compliance failed {(int)resp.StatusCode} (XID={xid}): {body}");
        }
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.TryGetProperty("secret", out var secretProp);
        json.TryGetProperty("requestID", out var requestIdProp);
        json.TryGetProperty("binarySecurityToken", out var tokenProp);

        string requestId = requestIdProp.ValueKind == JsonValueKind.Number ? requestIdProp.GetInt64().ToString() : requestIdProp.GetString() ?? string.Empty;
        string secret = secretProp.GetString() ?? string.Empty;
        string token = tokenProp.GetString() ?? string.Empty;
        return new ComplianceResponse(requestId, secret, token, json);
    }

    public async Task<ProductionCsidResponse> RequestProductionCsidAsync(string complianceRequestId, string binarySecurityToken, string secret, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/e-invoicing/developer-portal/production/csids");
        msg.Headers.TryAddWithoutValidation("accept", "application/json");
        msg.Headers.TryAddWithoutValidation("Accept-Version", "V2");
        msg.Headers.TryAddWithoutValidation("accept-language", "en");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{binarySecurityToken}:{secret}")));
        var payload = new { compliance_request_id = complianceRequestId };
        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Production CSID failed {(int)resp.StatusCode}: {body}");
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.TryGetProperty("binarySecurityToken", out var tokenProp);
        json.TryGetProperty("secret", out var secretProp);
        return new ProductionCsidResponse(tokenProp.GetString() ?? string.Empty, secretProp.ValueKind == JsonValueKind.Undefined ? secret : (secretProp.GetString() ?? secret), json);
    }

    public async Task<ProductionCsidResponse> RenewProductionCsidAsync(string binarySecurityToken, string secret, string csrBase64, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(new HttpMethod("PATCH"), "/e-invoicing/developer-portal/production/csids");
        msg.Headers.TryAddWithoutValidation("accept", "application/json");
        msg.Headers.TryAddWithoutValidation("Accept-Version", "V2");
        msg.Headers.TryAddWithoutValidation("accept-language", "en");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{binarySecurityToken}:{secret}")));
        var payload = new { csr = EnsurePemWrappedBase64(csrBase64) };
        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Production CSID renewal failed {(int)resp.StatusCode}: {body}");
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.TryGetProperty("binarySecurityToken", out var tokenProp);
        json.TryGetProperty("secret", out var secretProp);
        var newToken = tokenProp.ValueKind == JsonValueKind.String ? tokenProp.GetString()! : string.Empty;
        var newSecret = secretProp.ValueKind == JsonValueKind.String ? secretProp.GetString()! : secret;
        return new ProductionCsidResponse(newToken, newSecret, json);
    }

    public async Task<JsonElement> ComplianceCheckAsync(string binarySecurityToken, string secret, JsonElement requestApi, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/e-invoicing/developer-portal/compliance/invoices");
        msg.Headers.TryAddWithoutValidation("accept", "application/json");
        msg.Headers.TryAddWithoutValidation("Accept-Version", "V2");
        msg.Headers.TryAddWithoutValidation("accept-language", "en");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{binarySecurityToken}:{secret}")));
        msg.Content = new StringContent(JsonSerializer.Serialize(requestApi), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Compliance check failed {(int)resp.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    public async Task<JsonElement> ClearanceAsync(string invoiceHash, string uuid, string ublBase64, string invoiceType, string binarySecurityToken, string secret, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/e-invoicing/developer-portal/invoices/clearance/single");
        msg.Headers.Add("Accept-Version", "V2");
        msg.Headers.Add("accept", "application/json");
        msg.Headers.Add("accept-language", "en");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{binarySecurityToken}:{secret}")));
        var payload = new { invoiceHash, uuid, invoice = ublBase64, invoiceType = string.IsNullOrWhiteSpace(invoiceType) ? "Standard" : invoiceType, generationTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string xid = resp.Headers.TryGetValues("X-Global-Transaction-ID", out var vals) ? string.Join(",", vals) : string.Empty;
            throw new InvalidOperationException($"Clearance failed {(int)resp.StatusCode} (XID={xid}): {body}");
        }
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    public async Task<JsonElement> ReportingAsync(string invoiceHash, string uuid, string ublBase64, string invoiceType, string binarySecurityToken, string secret, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/e-invoicing/developer-portal/invoices/reporting/single");
        msg.Headers.Add("Accept-Version", "V2");
        msg.Headers.Add("accept", "application/json");
        msg.Headers.Add("accept-language", "en");
        if (_useSandboxHeaders) msg.Headers.Add("Clearance-Status", "0");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{binarySecurityToken}:{secret}")));
        var payload = new { invoiceHash, uuid, invoice = ublBase64, invoiceType = string.IsNullOrWhiteSpace(invoiceType) ? "Simplified" : invoiceType, generationTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(msg, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string xid = resp.Headers.TryGetValues("X-Global-Transaction-ID", out var vals) ? string.Join(",", vals) : string.Empty;
            throw new InvalidOperationException($"Reporting failed {(int)resp.StatusCode} (XID={xid}): {body}");
        }
        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}



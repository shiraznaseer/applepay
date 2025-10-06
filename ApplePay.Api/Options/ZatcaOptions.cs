namespace ApplePay.Api.Options;

public sealed class ZatcaOptions
{
	public const string SectionName = "Zatca";

	public string? BaseUrl { get; set; }
	public string? ComplianceUrl { get; set; }
	public string? ClearanceUrl { get; set; }
	public string? ReportingUrl { get; set; }
    public bool UseSandboxHeaders { get; set; }
	public int HttpTimeoutSeconds { get; set; } = 100;

	public string? ClientId { get; set; }
	public string? ClientSecret { get; set; }

	public string? Tin { get; set; }
}



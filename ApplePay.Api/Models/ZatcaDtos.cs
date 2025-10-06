namespace ApplePay.Api.Models;

public sealed class ZatcaIssueCsidRequest
{
    public string? Tin { get; set; }
    public string? OrganizationName { get; set; }
    public string? BranchName { get; set; }
    public string? CsrBase64 { get; set; }
    public string? Environment { get; set; }
}

public sealed class ZatcaIssueCsidResponse
{
    public bool Success { get; set; }
    public string? Csid { get; set; }
    public string? Error { get; set; }
}

public sealed class ZatcaInvoiceRequest
{
    public string? InvoiceXmlBase64 { get; set; }
    public bool Clearance { get; set; }
    public string? PreviousInvoiceHash { get; set; }
    public string? InvoiceType { get; set; }
}

public sealed class ZatcaInvoiceSubmitResponse
{
    public bool Success { get; set; }
    public string? InvoiceHash { get; set; }
    public string? ClearanceStatus { get; set; }
    public string? ReportingStatus { get; set; }
    public string? Error { get; set; }
}

public sealed class ZatcaInvoiceStatusResponse
{
    public bool Success { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
}



using ApplePay.Api.Models;
using ApplePay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace ApplePay.Api.Controllers;

[ApiController]
[Route("api/zatca")]
public sealed class ZatcaController : ControllerBase
{
    private readonly IZatcaService _zatcaService;
    private readonly ZatcaClient _client;
    private readonly QrService _qr;
    private readonly CertificateService _certs;
    private readonly XmlSigningService _xmlSigning;

    public ZatcaController(IZatcaService zatcaService, ZatcaClient client, QrService qr, CertificateService certs, XmlSigningService xmlSigning)
    {
        _zatcaService = zatcaService;
        _client = client;
        _qr = qr;
        _certs = certs;
        _xmlSigning = xmlSigning;
    }

    [HttpPost("csid/issue")]
    public async Task<ActionResult<ZatcaIssueCsidResponse>> IssueCsid([FromBody] ZatcaIssueCsidRequest request, CancellationToken cancellationToken)
    {
        var result = await _zatcaService.IssueCsidAsync(request, cancellationToken);
        if (!result.Success)
            return StatusCode(502, result);
        return Ok(result);
    }

    public sealed class SignRequest { public string Xml { get; set; } = string.Empty; public string PfxBase64 { get; set; } = string.Empty; public string PfxPassword { get; set; } = string.Empty; }
    [HttpPost("utils/sign")] 
    public ActionResult<object> SignXml([FromBody] SignRequest req)
    {
        var pfx = string.IsNullOrWhiteSpace(req.PfxBase64) ? Array.Empty<byte>() : Convert.FromBase64String(req.PfxBase64);
        var signed = _xmlSigning.SignXmlIfPossible(req.Xml, pfx, req.PfxPassword);
        return Ok(new { xml = signed });
    }

    public sealed class HashRequest { public string Xml { get; set; } = string.Empty; public bool ExcludeQr { get; set; } = true; }
    [HttpPost("utils/hash")] 
    public ActionResult<object> ComputeHash([FromBody] HashRequest req)
    {
        var hash = _xmlSigning.ComputeInvoiceHashBase64(req.Xml, req.ExcludeQr);
        return Ok(new { invoiceHash = hash });
    }

    // Utilities
    public sealed class QrRequest { public string SellerName { get; set; } = string.Empty; public string VatNumber { get; set; } = string.Empty; public string IsoTimestamp { get; set; } = string.Empty; public string TotalWithVat { get; set; } = string.Empty; public string VatAmount { get; set; } = string.Empty; }
    [HttpPost("utils/qr")] 
    public ActionResult<object> BuildQr([FromBody] QrRequest req)
    {
        var base64 = _qr.BuildTlvQrBase64(req.SellerName, req.VatNumber, req.IsoTimestamp, req.TotalWithVat, req.VatAmount);
        return Ok(new { base64 });
    }

    public sealed class CsrDto
    {
        public string CommonName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string OrganizationIdentifier { get; set; } = string.Empty;
        public string OrganizationUnitName { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string CountryName { get; set; } = "SA";
        public string InvoiceType { get; set; } = "1100";
        public string LocationAddress { get; set; } = string.Empty;
        public string IndustryBusinessCategory { get; set; } = string.Empty;
    }
    [HttpPost("utils/csr/nonprod")] 
    public ActionResult<object> GenerateCsr([FromBody] CsrDto dto)
    {
        var res = _certs.GenerateCsrNonProduction(dto.CommonName, dto.SerialNumber, dto.OrganizationIdentifier, dto.OrganizationUnitName, dto.OrganizationName, dto.CountryName, dto.InvoiceType, dto.LocationAddress, dto.IndustryBusinessCategory);
        return Ok(res);
    }

    // --- ZATCA Compliance & CSID endpoints ---
    public sealed class ComplianceStartRequest { public string CsrBase64 { get; set; } = string.Empty; public string Otp { get; set; } = string.Empty; }
    [HttpPost("compliance/start")]
    public async Task<ActionResult<object>> StartCompliance([FromBody] ComplianceStartRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _client.RequestComplianceAsync(request.CsrBase64, request.Otp, cancellationToken);
            return Ok(new { requestId = res.RequestId, secret = res.Secret, binarySecurityToken = res.BinarySecurityToken, raw = res.RawJson });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public sealed class ProductionCsidRequest { public string ComplianceRequestId { get; set; } = string.Empty; public string BinarySecurityToken { get; set; } = string.Empty; public string Secret { get; set; } = string.Empty; }
    [HttpPost("production/csids")]
    public async Task<ActionResult<object>> RequestProductionCsid([FromBody] ProductionCsidRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _client.RequestProductionCsidAsync(request.ComplianceRequestId, request.BinarySecurityToken, request.Secret, cancellationToken);
            return Ok(new { binarySecurityToken = res.BinarySecurityToken, secret = res.Secret, raw = res.RawJson });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public sealed class RenewCsidRequest { public string BinarySecurityToken { get; set; } = string.Empty; public string Secret { get; set; } = string.Empty; public string CsrBase64 { get; set; } = string.Empty; }
    [HttpPatch("production/csids")]
    public async Task<ActionResult<object>> RenewProductionCsid([FromBody] RenewCsidRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _client.RenewProductionCsidAsync(request.BinarySecurityToken, request.Secret, request.CsrBase64, cancellationToken);
            return Ok(new { binarySecurityToken = res.BinarySecurityToken, secret = res.Secret, raw = res.RawJson });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("compliance/invoices")]
    public async Task<ActionResult<object>> ComplianceInvoices([FromQuery] string binarySecurityToken, [FromQuery] string secret, [FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _client.ComplianceCheckAsync(binarySecurityToken, secret, payload, cancellationToken);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public sealed class ClearanceRequest { public string InvoiceHash { get; set; } = string.Empty; public string Uuid { get; set; } = string.Empty; public string UblBase64 { get; set; } = string.Empty; public string InvoiceType { get; set; } = "Standard"; public string BinarySecurityToken { get; set; } = string.Empty; public string Secret { get; set; } = string.Empty; }
    [HttpPost("invoices/clearance")] 
    public async Task<ActionResult<object>> Clearance([FromBody] ClearanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _client.ClearanceAsync(request.InvoiceHash, request.Uuid, request.UblBase64, request.InvoiceType, request.BinarySecurityToken, request.Secret, cancellationToken);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public sealed class ReportingRequest { public string InvoiceHash { get; set; } = string.Empty; public string Uuid { get; set; } = string.Empty; public string UblBase64 { get; set; } = string.Empty; public string InvoiceType { get; set; } = "Simplified"; public string BinarySecurityToken { get; set; } = string.Empty; public string Secret { get; set; } = string.Empty; }
    [HttpPost("invoices/reporting")] 
    public async Task<ActionResult<object>> Reporting([FromBody] ReportingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _client.ReportingAsync(request.InvoiceHash, request.Uuid, request.UblBase64, request.InvoiceType, request.BinarySecurityToken, request.Secret, cancellationToken);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("invoices/submit")]
    public async Task<ActionResult<ZatcaInvoiceSubmitResponse>> SubmitInvoice([FromBody] ZatcaInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _zatcaService.SubmitInvoiceAsync(request, cancellationToken);
        if (!result.Success)
            return StatusCode(502, result);
        return Ok(result);
    }

    [HttpGet("invoices/{invoiceHash}/status")]
    public async Task<ActionResult<ZatcaInvoiceStatusResponse>> GetInvoiceStatus([FromRoute] string invoiceHash, CancellationToken cancellationToken)
    {
        var result = await _zatcaService.GetInvoiceStatusAsync(invoiceHash, cancellationToken);
        if (!result.Success)
            return StatusCode(502, result);
        return Ok(result);
    }
}



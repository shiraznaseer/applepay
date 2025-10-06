using ApplePay.Api.Models;

namespace ApplePay.Api.Services;

public interface IZatcaService
{
    Task<ZatcaIssueCsidResponse> IssueCsidAsync(ZatcaIssueCsidRequest request, CancellationToken cancellationToken);
    Task<ZatcaInvoiceSubmitResponse> SubmitInvoiceAsync(ZatcaInvoiceRequest request, CancellationToken cancellationToken);
    Task<ZatcaInvoiceStatusResponse> GetInvoiceStatusAsync(string invoiceHash, CancellationToken cancellationToken);
}



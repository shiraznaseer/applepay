using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography.X509Certificates;

namespace ApplePay.Models
{
    public sealed class AppleMerchantCertificate
    {
        private readonly X509Certificate2 merchantCertificate;

        // Constructor: Load from base64-encoded certificate and password
        public AppleMerchantCertificate(string base64Certificate, string certificatePassword)
        {
            merchantCertificate = LoadCertificate(base64Certificate, certificatePassword);
        }

        // Constructor: Accept existing certificate directly
        public AppleMerchantCertificate(X509Certificate2 certificate)
        {
            merchantCertificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        }

        // Return the loaded certificate
        public X509Certificate2 GetCertificate()
        {
            return merchantCertificate;
        }

        // Extract the merchant identifier from the certificate
        public string GetMerchantIdentifier()
        {
            try
            {
                return ExtractMerchantIdentifier(merchantCertificate);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // Private method to extract the Apple Merchant Identifier from the certificate
        private string ExtractMerchantIdentifier(X509Certificate2 certificate)
        {
            const string AppleMerchantIdOid = "1.2.840.113635.100.6.32";

            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            var extension = certificate.Extensions[AppleMerchantIdOid];
            if (extension == null)
                throw new InvalidOperationException("Merchant identifier extension not found in certificate.");

            var rawData = extension.RawData;
            if (rawData.Length <= 2)
                throw new InvalidOperationException("Invalid merchant identifier data.");

            // Skip the first two ASN.1 metadata bytes
            return Encoding.ASCII.GetString(rawData.Skip(2).ToArray());
        }

        // Load certificate from base64 string
        private X509Certificate2 LoadCertificate(string base64Certificate, string certificatePassword)
        {
            try
            {
                return new X509Certificate2(Convert.FromBase64String(base64Certificate), certificatePassword);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load Apple Pay merchant certificate.", ex);
            }
        }
    }
    public class AppleMerchantSessionRequest
    {
        public string merchantIdentifier { get; set; }
        public string displayName { get; set; }
        public string initiative { get; set; } = "web";
        public string initiativeContext { get; set; }
    }

}

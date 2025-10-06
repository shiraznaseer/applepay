using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using System.Text;

namespace ApplePay.Api.Services;

public sealed class CertificateService
{
    public sealed record CsrResult(string CsrBase64, string PrivateKeyBase64, string Subject, string Pem);

    public enum EnvironmentType { Production, Simulation, NonProduction }

    public CsrResult GenerateCsrNonProduction(
        string commonName,
        string serialNumber,
        string organizationIdentifier,
        string organizationUnitName,
        string organizationName,
        string countryName,
        string invoiceType,
        string locationAddress,
        string industryBusinessCategory)
    {
        return GenerateCsrWithAttributes(
            commonName, serialNumber, organizationIdentifier, organizationUnitName,
            organizationName, countryName, invoiceType, locationAddress, industryBusinessCategory,
            EnvironmentType.NonProduction);
    }

    public CsrResult GenerateCsrWithAttributes(
        string commonName,
        string serialNumber,
        string organizationIdentifier,
        string organizationUnitName,
        string organizationName,
        string countryName,
        string invoiceType,
        string locationAddress,
        string industryBusinessCategory,
        EnvironmentType environment)
    {
        var subjectOids = new[] { X509Name.C, X509Name.OU, X509Name.O, X509Name.CN };
        var subjectVals = new[]
        {
            countryName ?? "SA",
            organizationUnitName ?? string.Empty,
            organizationName ?? string.Empty,
            commonName ?? string.Empty
        };
        var subject = new X509Name(subjectOids, subjectVals);

        var sanOids = new[] { X509Name.Surname, X509Name.UID, X509Name.T, new DerObjectIdentifier("2.5.4.26"), X509Name.BusinessCategory };
        var sanVals = new[]
        {
            serialNumber ?? string.Empty,
            organizationIdentifier ?? string.Empty,
            invoiceType ?? string.Empty,
            locationAddress ?? string.Empty,
            industryBusinessCategory ?? string.Empty
        };
        var san = new X509Name(sanOids, sanVals);

        string templateName = environment switch
        {
            EnvironmentType.Production => "ZATCA-Code-Signing",
            EnvironmentType.Simulation => "PREZATCA-Code-Signing",
            _ => "TSTZATCA-Code-Signing"
        };

        var extDict = new Dictionary<DerObjectIdentifier, X509Extension>
        {
            { new DerObjectIdentifier("1.3.6.1.4.1.311.20.2"), new X509Extension(false, new DerOctetString(new DerPrintableString(templateName))) },
            { X509Extensions.SubjectAlternativeName, new X509Extension(false, new DerOctetString(new DerSequence(new DerTaggedObject(4, san)))) }
        };
        var x509Exts = new X509Extensions(extDict);
        var attrs = new DerSet(new Org.BouncyCastle.Asn1.Pkcs.AttributePkcs(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(x509Exts)));

        var keyGen = new ECKeyPairGenerator();
        keyGen.Init(new ECKeyGenerationParameters(SecObjectIdentifiers.SecP256r1, new SecureRandom()));
        var keyPair = keyGen.GenerateKeyPair();

        var csr = new Pkcs10CertificationRequest("SHA256withECDSA", subject, keyPair.Public, attrs, keyPair.Private);

        string csrPem;
        using (var sw = new StringWriter())
        {
            var pemWriter = new PemWriter(sw);
            pemWriter.WriteObject(csr);
            pemWriter.Writer.Flush();
            csrPem = sw.ToString();
        }
        var csrBase64OfPem = Convert.ToBase64String(Encoding.ASCII.GetBytes(csrPem));

        var keyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);
        var pkcs8Der = keyInfo.ToAsn1Object().GetDerEncoded();
        var privateKeyBase64 = Convert.ToBase64String(pkcs8Der);

        return new CsrResult(csrBase64OfPem, privateKeyBase64, subject.ToString(), csrPem);
    }
}



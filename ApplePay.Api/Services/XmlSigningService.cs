using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace ApplePay.Api.Services;

public sealed class XmlSigningService
{
    public string SignXmlIfPossible(string xml, byte[] pfxBytes, string pfxPassword)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML cannot be null or empty", nameof(xml));

        if (pfxBytes == null || pfxBytes.Length == 0 || string.IsNullOrEmpty(pfxPassword))
            return xml;

        try
        {
            X509Certificate2 certificate;
            try
            {
                certificate = new X509Certificate2(pfxBytes, pfxPassword,
                    X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException)
            {
                certificate = new X509Certificate2(pfxBytes, pfxPassword,
                    X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
            }

            var ecdsa = certificate.GetECDsaPrivateKey();
            var rsa = certificate.GetRSAPrivateKey();
            if (ecdsa == null && rsa == null) return xml;

            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xml);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("inv", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            nsMgr.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
            nsMgr.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
            nsMgr.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");

            var root = doc.DocumentElement!;
            var extNode = root.SelectSingleNode("inv:UBLExtensions", nsMgr) as XmlElement;
            if (extNode == null)
            {
                extNode = doc.CreateElement("ext", "UBLExtensions", nsMgr.LookupNamespace("ext"));
                root.InsertBefore(extNode, root.FirstChild);
            }

            var ubExt = doc.CreateElement("ext", "UBLExtension", nsMgr.LookupNamespace("ext"));
            var extContent = doc.CreateElement("ext", "ExtensionContent", nsMgr.LookupNamespace("ext"));
            ubExt.AppendChild(extContent);
            extNode.AppendChild(ubExt);

            SignedXml signedXml;
            if (ecdsa != null)
            {
                signedXml = new SignedXml(doc) { SigningKey = ecdsa };
                signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
            }
            else
            {
                signedXml = new SignedXml(doc) { SigningKey = rsa };
                signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
            }

            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

            var reference = new Reference { Uri = string.Empty };
            reference.AddTransform(BuildSignedXPathTransform("not(ancestor-or-self::ext:UBLExtensions)"));
            reference.AddTransform(BuildSignedXPathTransform("not(ancestor-or-self::cac:AdditionalDocumentReference[cbc:ID='QR'])"));
            reference.AddTransform(new XmlDsigExcC14NTransform());
            reference.DigestMethod = SignedXml.XmlDsigSHA256Url;
            signedXml.AddReference(reference);

            var keyInfo = new KeyInfo();
            var x509Data = new KeyInfoX509Data(certificate);
            x509Data.AddIssuerSerial(certificate.Issuer, certificate.SerialNumber);
            keyInfo.AddClause(x509Data);
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();
            var xmlDigitalSignature = signedXml.GetXml();
            extContent.AppendChild(doc.ImportNode(xmlDigitalSignature, true));
            return doc.OuterXml;
        }
        catch
        {
            return xml;
        }
    }

    public string ComputeInvoiceHashBase64(string xml, bool excludeQr)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML cannot be null or empty", nameof(xml));

        var cleanXml = xml.Trim().Replace("\uFEFF", string.Empty).Replace("\0", string.Empty);
        cleanXml = cleanXml.Replace("\r\n", "\n").Replace("\n", "\r\n");

        var transformedXml = RemoveNodesForHashing(cleanXml, excludeQr);

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        xmlDoc.LoadXml(transformedXml);
        var c14n = new XmlDsigC14NTransform(false);
        c14n.LoadInput(xmlDoc);

        using var outputStream = (Stream)c14n.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        outputStream.CopyTo(ms);
        var canonicalBytes = ms.ToArray();
        var hashBytes = SHA256.HashData(canonicalBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static object ApplyXPathTransform(object input, string xpathExpression)
    {
        var xmlDoc = new XmlDocument();
        var root = xmlDoc.CreateElement("Root");
        var xpathNode = xmlDoc.CreateElement("XPath");
        xpathNode.SetAttribute("xmlns:ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        xpathNode.SetAttribute("xmlns:cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        xpathNode.SetAttribute("xmlns:cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        xpathNode.InnerText = xpathExpression;
        root.AppendChild(xpathNode);
        xmlDoc.AppendChild(root);
        var transform = new XmlDsigXPathTransform();
        transform.LoadInnerXml(root.ChildNodes);
        transform.LoadInput(input);
        return transform.GetOutput();
    }

    private static Transform BuildSignedXPathTransform(string xpathExpression)
    {
        var xmlDoc = new XmlDocument();
        var root = xmlDoc.CreateElement("Root");
        var xpathNode = xmlDoc.CreateElement("XPath");
        xpathNode.SetAttribute("xmlns:ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        xpathNode.SetAttribute("xmlns:cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        xpathNode.SetAttribute("xmlns:cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        xpathNode.InnerText = xpathExpression;
        root.AppendChild(xpathNode);
        xmlDoc.AppendChild(root);
        var transform = new XmlDsigXPathTransform();
        transform.LoadInnerXml(root.ChildNodes);
        return transform;
    }

    private static string RemoveNodesForHashing(string xml, bool excludeQr)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xml);

        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        nsMgr.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        nsMgr.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

        var ublExtensions = doc.SelectNodes("//ext:UBLExtensions", nsMgr);
        foreach (XmlNode node in ublExtensions!) node.ParentNode?.RemoveChild(node);

        if (excludeQr)
        {
            var qrNodes = doc.SelectNodes("//cac:AdditionalDocumentReference[cbc:ID='QR']", nsMgr);
            foreach (XmlNode node in qrNodes!) node.ParentNode?.RemoveChild(node);
        }

        return doc.OuterXml;
    }
}



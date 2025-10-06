using System.Text;

namespace ApplePay.Api.Services;

public sealed class QrService
{
    public string BuildTlvQrBase64(string sellerName, string vatNumber, string isoTimestamp, string totalWithVat, string vatAmount)
    {
        byte[] tlv = BuildTlv(
            (1, sellerName),
            (2, vatNumber),
            (3, isoTimestamp),
            (4, totalWithVat),
            (5, vatAmount)
        );
        return Convert.ToBase64String(tlv);
    }

    private static byte[] BuildTlv(params (int tag, string value)[] fields)
    {
        using var ms = new MemoryStream();
        foreach (var (tag, value) in fields)
        {
            var valBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            ms.WriteByte((byte)tag);
            var len = (byte)valBytes.Length;
            ms.WriteByte(len);
            ms.Write(valBytes, 0, valBytes.Length);
        }
        return ms.ToArray();
    }
}



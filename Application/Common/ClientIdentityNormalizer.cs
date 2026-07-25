using System.Security.Cryptography;
using System.Text;

namespace Application.Common;

public static class ClientIdentityNormalizer
{
    public static string Name(string? value) => string.Join(' ', (value ?? string.Empty)
        .Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static string Email(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static string? Phone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    public static string Invoice(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static string Fingerprint(string? fullName, string? email, string? phone, string? invoiceNumber)
    {
        var canonical = string.Join('|', Name(fullName), Email(email), Phone(phone) ?? string.Empty,
            Invoice(invoiceNumber));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

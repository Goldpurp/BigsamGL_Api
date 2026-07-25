using System.Security.Cryptography;

namespace Application.Common;

public static class DocumentSecurity
{
    public static string NewVerificationCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}


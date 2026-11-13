using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace RefundPilot.Infrastructure;

/// <summary>Encrypts the account number at rest with ASP.NET Data Protection.</summary>
public sealed class IbanCipher(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("RefundPilot.Profile.Iban");

    public string? Protect(string? plain) => string.IsNullOrEmpty(plain) ? null : _protector.Protect(plain);

    /// <summary>Returns null (instead of throwing) for a value that cannot be decrypted, e.g. after the keys were lost.</summary>
    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return null;
        try
        {
            return _protector.Unprotect(stored);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Store.Api.Auth;

/// <summary>
/// Both password representations (ADR-0011 dual columns). Registration stores both so VULN-PWD-01
/// can switch between them at runtime. The "legacy" form is deliberately weak; the "secure" form
/// is the one a real store should use.
/// </summary>
public static class PasswordHashing
{
    private static readonly PasswordHasher<object> SecureHasher = new();
    private static readonly object HashSubject = new();

    /// <summary>
    /// VULNERABLE: a fast, unsalted SHA-256 hash. Identical passwords produce identical hashes, so a
    /// leak of this column is trivially reversible with a rainbow table or a wordlist (asset A4).
    /// </summary>
    public static string LegacyHash(string password) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public static bool VerifyLegacy(string? hash, string password) =>
        hash is not null &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(hash),
            Encoding.ASCII.GetBytes(LegacyHash(password)));

    /// <summary>SECURE: ASP.NET Core's PBKDF2 hasher: per-password salt and many iterations.</summary>
    public static string SecureHash(string password) =>
        SecureHasher.HashPassword(HashSubject, password);

    public static bool VerifySecure(string? hash, string password) =>
        hash is not null &&
        SecureHasher.VerifyHashedPassword(HashSubject, hash, password) != PasswordVerificationResult.Failed;
}

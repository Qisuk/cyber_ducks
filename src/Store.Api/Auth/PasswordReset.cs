using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Store.Api.Auth;

/// <summary>
/// In-memory store of outstanding password-reset tokens (fine for the single-instance range). A token
/// maps to a user id and an expiry; consuming it removes it (single use).
/// </summary>
public sealed class PasswordResetStore
{
    private readonly ConcurrentDictionary<string, Entry> _tokens = new();

    public void Add(string token, int userId, TimeSpan ttl) =>
        _tokens[token] = new Entry(userId, DateTimeOffset.UtcNow + ttl);

    /// <summary>Returns the user id for a valid, unexpired token and removes it; null otherwise.</summary>
    public int? Consume(string token)
    {
        if (_tokens.TryRemove(token, out var entry) && entry.Expiry > DateTimeOffset.UtcNow)
        {
            return entry.UserId;
        }
        return null;
    }

    /// <summary>
    /// VULN-RESET-01. Vulnerable mode issues a short, guessable 6-digit token (brute-forceable, and
    /// cheap to use if the mail is intercepted); secure issues a 256-bit URL-safe random token.
    /// </summary>
    public static string NewToken(bool secure) => secure
        ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        : RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private readonly record struct Entry(int UserId, DateTimeOffset Expiry);
}

public sealed record ForgotPasswordRequest(string? Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);

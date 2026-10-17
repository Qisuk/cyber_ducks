using System.Security.Cryptography;
using System.Text;

namespace Store.Api.Payments;

/// <summary>Checks a payment webhook's signature — VULN-WEBHOOK-01.</summary>
public interface IWebhookVerifier
{
    bool Verify(string payload, string? signature);
}

/// <summary>
/// VULNERABLE (VULN-WEBHOOK-01): accepts any webhook, signed or not. Anyone who can reach the
/// endpoint can mark an invoice paid without paying (threat CT40, asset A14).
/// </summary>
public sealed class VulnerableWebhookVerifier : IWebhookVerifier
{
    public bool Verify(string payload, string? signature) => true;
}

/// <summary>SECURE (VULN-WEBHOOK-01): the body must carry a valid HMAC-SHA256 signature.</summary>
public sealed class SecureWebhookVerifier(WebhookOptions options) : IWebhookVerifier
{
    public bool Verify(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }
        var expected = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Secret), Encoding.UTF8.GetBytes(payload)));
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected), Convert.FromHexString(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record WebhookOptions(string Secret);

namespace Store.Api.Auth;

/// <summary>Whether a social login may be linked to an existing local account by email — VULN-OAUTH-03.</summary>
public interface ISocialLinking
{
    bool MayLinkByEmail(bool emailVerified);
}

/// <summary>
/// VULNERABLE (VULN-OAUTH-03): links to a local account on a matching email even when the provider
/// did not verify it. An attacker who creates a "Meta" account with someone else's email (unverified)
/// takes over that local account (threat CT19, assets A4/A12).
/// </summary>
public sealed class VulnerableSocialLinking : ISocialLinking
{
    public bool MayLinkByEmail(bool emailVerified) => true;
}

/// <summary>SECURE (VULN-OAUTH-03): link by email only when the provider verified it.</summary>
public sealed class SecureSocialLinking : ISocialLinking
{
    public bool MayLinkByEmail(bool emailVerified) => emailVerified;
}

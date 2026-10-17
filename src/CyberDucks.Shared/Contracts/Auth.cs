namespace CyberDucks.Shared.Contracts;

// Auth contracts shared by store-api and store-web.

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>Issued on a successful login. The token is a JWT store-web forwards on each API call.</summary>
public sealed record AuthResult(string Token, DateTimeOffset ExpiresAt, UserInfo User);

public sealed record UserInfo(int Id, string Email, string DisplayName, string Role);

/// <summary>Profile edit. <see cref="Role"/> should never be honoured, but the vulnerable path does (VULN-MASS-01).</summary>
public sealed record UpdateProfileRequest(string DisplayName, string? Role);

/// <summary>
/// A verified social identity from store-web's OIDC flow. store-api finds/links/creates the local
/// account; whether it links on an unverified email is VULN-OAUTH-03.
/// </summary>
public sealed record SocialLoginRequest(string Provider, string Subject, string? Email, bool EmailVerified, string DisplayName);

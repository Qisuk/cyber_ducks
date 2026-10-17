namespace CyberDucks.Shared.Contracts;

// Auth contracts shared by store-api and store-web.

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

/// <summary>Issued on a successful login. The token is a JWT store-web forwards on each API call.</summary>
public sealed record AuthResult(string Token, DateTimeOffset ExpiresAt, UserInfo User);

public sealed record UserInfo(int Id, string Email, string DisplayName, string Role);

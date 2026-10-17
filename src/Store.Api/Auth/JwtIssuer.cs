using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Store.Api.Data;

namespace Store.Api.Auth;

public sealed record JwtOptions
{
    public required string Secret { get; init; }
    public string Issuer { get; init; } = "cyber-ducks";
    public string Audience { get; init; } = "cyber-ducks";
    public int LifetimeMinutes { get; init; } = 30;
}

/// <summary>
/// Issues the JWT that store-web holds and forwards to store-api on every call (flow F4). The
/// secure baseline here: HS256 with a configured secret, short lifetime, explicit issuer/audience.
/// VULN-SESSION-01 (step 3d) adds weakened variants beside it.
/// </summary>
public sealed class JwtIssuer(JwtOptions options, TimeProvider clock)
{
    public const string RoleClaim = "role";

    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
        SecurityAlgorithms.HmacSha256);

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(options.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Name] = user.DisplayName,
                [RoleClaim] = user.Role,
            },
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

using System.Security.Cryptography;
using System.Text;
using CyberDucks.Shared.Defences;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Store.Api.Auth;

/// <summary>
/// JWT signature validation behind VULN-SESSION-01. Plugged into JwtBearer as the
/// <see cref="TokenValidationParameters.SignatureValidator"/>, so it runs per token and can read
/// the live flag.
/// Vulnerable: the signature is NOT checked, so an attacker can forge a token with any claims
/// (e.g. role=admin, any sub) — including an unsigned "alg:none" token (threat CT1/A12).
/// Secure: the HS256 signature is verified against the configured key.
/// </summary>
public static class TogglingSignatureValidator
{
    public static SecurityToken Validate(string token, TokenValidationParameters parameters, IDefenceFlags flags)
    {
        var jwt = new JsonWebToken(token);

        if (flags.IsSecure("VULN-SESSION-01"))
        {
            var key = (SymmetricSecurityKey)parameters.IssuerSigningKey;
            var signingInput = Encoding.ASCII.GetBytes($"{jwt.EncodedHeader}.{jwt.EncodedPayload}");
            var expected = HMACSHA256.HashData(key.Key, signingInput);

            byte[] actual;
            try
            {
                actual = Base64UrlEncoder.DecodeBytes(jwt.EncodedSignature);
            }
            catch
            {
                throw new SecurityTokenInvalidSignatureException("Malformed signature.");
            }

            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                throw new SecurityTokenInvalidSignatureException("Signature validation failed.");
            }
        }

        return jwt;
    }
}

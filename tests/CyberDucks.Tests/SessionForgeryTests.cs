using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CyberDucks.Tests;

/// <summary>VULN-SESSION-01: whether forged (wrongly signed) tokens are accepted.</summary>
public sealed class SessionForgeryTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // A token with the right issuer/audience and admin claims, signed with an attacker's key
    // (not the server's). Only a missing signature check would accept it.
    private static string ForgedAdminToken()
    {
        var attackerKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("attacker-key-attacker-key-32bytes!!!!"));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "cyber-ducks",
            Audience = "cyber-ducks",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(attackerKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "1",
                ["email"] = "attacker@evil.test",
                ["name"] = "Mallory",
                ["role"] = "admin",
            },
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Vulnerable_accepts_a_forged_token()
    {
        _factory.SetFlag("VULN-SESSION-01", DefenceMode.Vulnerable);

        var me = await ClientWith(ForgedAdminToken()).GetFromJsonAsync<UserInfo>("/api/auth/me", Ct);

        Assert.Equal("admin", me!.Role); // forged privilege accepted
    }

    [Fact]
    public async Task Secure_rejects_a_forged_token()
    {
        _factory.SetFlag("VULN-SESSION-01", DefenceMode.Secure);

        var response = await ClientWith(ForgedAdminToken()).GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Secure_still_accepts_a_genuine_token()
    {
        _factory.SetFlag("VULN-SESSION-01", DefenceMode.Secure);

        var me = await ClientWith(await _factory.TokenForAsync(1)).GetFromJsonAsync<UserInfo>("/api/auth/me", Ct);

        Assert.Equal(1, me!.Id);
    }

    public void Dispose() => _factory.Dispose();
}

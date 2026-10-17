using System.Net;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

/// <summary>VULN-OAUTH-03: linking a social login to a local account by email.</summary>
public sealed class SocialLoginTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // Registers a local account and returns its email.
    private async Task<string> RegisterLocalAsync()
    {
        var email = $"victim-{Guid.NewGuid():N}@example.test";
        var reg = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Victim", "Correct-Horse-1"), Ct);
        reg.EnsureSuccessStatusCode();
        return email;
    }

    private Task<HttpResponseMessage> SocialAsync(string provider, string subject, string email, bool verified) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/social",
            new SocialLoginRequest(provider, subject, email, verified, "Social User"), Ct);

    [Fact]
    public async Task Vulnerable_links_an_unverified_social_login_to_an_existing_account()
    {
        _factory.SetFlag("VULN-OAUTH-03", DefenceMode.Vulnerable);
        var email = await RegisterLocalAsync();

        // Attacker's social account claims the victim's email, unverified.
        var response = await SocialAsync("meta", Guid.NewGuid().ToString(), email, verified: false);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, result!.User.Email); // logged in AS the victim (takeover)
    }

    [Fact]
    public async Task Secure_rejects_an_unverified_social_login_for_an_existing_account()
    {
        _factory.SetFlag("VULN-OAUTH-03", DefenceMode.Secure);
        var email = await RegisterLocalAsync();

        var response = await SocialAsync("meta", Guid.NewGuid().ToString(), email, verified: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Verified_social_login_links_to_the_existing_account()
    {
        _factory.SetFlag("VULN-OAUTH-03", DefenceMode.Secure);
        var email = await RegisterLocalAsync();

        var response = await SocialAsync("google", Guid.NewGuid().ToString(), email, verified: true);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, result!.User.Email);
    }

    [Fact]
    public async Task A_brand_new_social_login_creates_a_customer()
    {
        var email = $"fresh-{Guid.NewGuid():N}@gmail.test";
        var response = await SocialAsync("google", Guid.NewGuid().ToString(), email, verified: true);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>(Ct);

        Assert.Equal(email, result!.User.Email);
        Assert.Equal("customer", result.User.Role);
    }

    [Fact]
    public async Task A_returning_social_identity_logs_into_the_same_account()
    {
        var subject = Guid.NewGuid().ToString();
        var email = $"returning-{Guid.NewGuid():N}@gmail.test";

        var first = await (await SocialAsync("google", subject, email, true)).Content.ReadFromJsonAsync<AuthResult>(Ct);
        var second = await (await SocialAsync("google", subject, email, true)).Content.ReadFromJsonAsync<AuthResult>(Ct);

        Assert.Equal(first!.User.Id, second!.User.Id);
    }

    public void Dispose() => _factory.Dispose();
}

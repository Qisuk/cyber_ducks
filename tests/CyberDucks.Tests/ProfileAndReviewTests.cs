using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

public sealed class ProfileAndReviewTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Client, string Email)> RegisterAndAuthAsync()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Test User", "Correct-Horse-1"), Ct);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (client, email);
    }

    [Fact]
    public async Task Vulnerable_profile_update_lets_a_customer_make_themselves_admin()
    {
        _factory.SetFlag("VULN-MASS-01", DefenceMode.Vulnerable);
        var (client, _) = await RegisterAndAuthAsync();

        var response = await client.PutAsJsonAsync("/api/auth/profile",
            new UpdateProfileRequest("Totally Normal", "admin"), Ct);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>(Ct);

        Assert.Equal("admin", result!.User.Role); // privilege escalation
    }

    [Fact]
    public async Task Secure_profile_update_ignores_the_role_field()
    {
        _factory.SetFlag("VULN-MASS-01", DefenceMode.Secure);
        var (client, _) = await RegisterAndAuthAsync();

        var response = await client.PutAsJsonAsync("/api/auth/profile",
            new UpdateProfileRequest("New Name", "admin"), Ct);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>(Ct);

        Assert.Equal("customer", result!.User.Role); // role unchanged
        Assert.Equal("New Name", result.User.DisplayName); // but the name did change
    }

    [Fact]
    public async Task Posted_review_body_is_stored_and_returned_verbatim()
    {
        // The API doesn't sanitise; encoding is store-web's job (VULN-XSS-01). This confirms the
        // review is a faithful carrier for injected markup (and later, prompt injection, TH8).
        var (client, _) = await RegisterAndAuthAsync();
        const string payload = "<script>alert('xss')</script> great duck";

        var post = await client.PostAsJsonAsync("/api/products/1/reviews", new PostReviewRequest(5, payload), Ct);
        post.EnsureSuccessStatusCode();

        var product = await client.GetFromJsonAsync<ProductDetail>("/api/products/1", Ct);
        Assert.Contains(product!.Reviews, r => r.Body == payload);
    }

    public void Dispose() => _factory.Dispose();
}

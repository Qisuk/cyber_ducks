using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

public sealed class AuthTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // Unique email per test so the shared database doesn't collide across tests.
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private async Task<string> RegisterAsync(HttpClient client, string email, string password = "Correct-Horse-1")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Test User", password), Ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResult>(Ct);
        return result!.Token;
    }

    private async Task<(HttpStatusCode Status, string? Message)> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Ct);
        if (response.IsSuccessStatusCode)
        {
            return (response.StatusCode, null);
        }
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(Ct);
        return (response.StatusCode, body?.GetValueOrDefault("message"));
    }

    [Theory]
    [InlineData(DefenceMode.Vulnerable)]
    [InlineData(DefenceMode.Secure)]
    public async Task Registered_user_can_log_in_in_either_password_mode(DefenceMode mode)
    {
        // Registration writes both password forms, so login works whichever column VULN-PWD-01 selects.
        _factory.SetFlag("VULN-PWD-01", mode);
        var client = _factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var (status, _) = await LoginAsync(client, email, "Correct-Horse-1");
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var (status, _) = await LoginAsync(client, email, "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Vulnerable_login_reveals_whether_the_account_exists()
    {
        _factory.SetFlag("VULN-AUTH-01", DefenceMode.Vulnerable);
        var client = _factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var unknown = await LoginAsync(client, NewEmail(), "whatever");
        var wrongPassword = await LoginAsync(client, email, "wrong");

        // User enumeration: the two cases give different messages.
        Assert.Contains("No account", unknown.Message);
        Assert.Contains("Incorrect password", wrongPassword.Message);
    }

    [Fact]
    public async Task Secure_login_gives_one_generic_message()
    {
        _factory.SetFlag("VULN-AUTH-01", DefenceMode.Secure);
        var client = _factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var unknown = await LoginAsync(client, NewEmail(), "whatever");
        var wrongPassword = await LoginAsync(client, email, "wrong");

        Assert.Equal("Invalid email or password.", unknown.Message);
        Assert.Equal("Invalid email or password.", wrongPassword.Message);
    }

    [Fact]
    public async Task Secure_mode_locks_the_account_after_repeated_failures()
    {
        _factory.SetFlag("VULN-AUTH-01", DefenceMode.Secure);
        var client = _factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(client, email, "wrong");
        }

        // Even the correct password is now refused: the account is locked.
        var (status, _) = await LoginAsync(client, email, "Correct-Horse-1");
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Vulnerable_mode_never_locks_the_account()
    {
        _factory.SetFlag("VULN-AUTH-01", DefenceMode.Vulnerable);
        var client = _factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        for (var i = 0; i < 10; i++)
        {
            await LoginAsync(client, email, "wrong");
        }

        // The correct password still works: unlimited attempts.
        var (status, _) = await LoginAsync(client, email, "Correct-Horse-1");
        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Me_requires_a_token_and_returns_the_current_user()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();
        var token = await RegisterAsync(client, email);

        var anonymous = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetFromJsonAsync<UserInfo>("/api/auth/me", Ct);
        Assert.Equal(email, me!.Email);
        Assert.Equal("customer", me.Role);
    }

    public void Dispose() => _factory.Dispose();
}

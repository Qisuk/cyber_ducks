using CyberDucks.Shared.Contracts;

namespace AgentTools;

/// <summary>
/// Obtains and caches a token for a single <b>shared service account</b> by logging in to store-api.
/// Used only by the VULN-MCP-01 <i>vulnerable</i> path: instead of acting as the signed-in customer,
/// agent-tools calls store-api as this one account for everybody — the confused-deputy anti-pattern.
/// The account and password come from configuration (fake range credentials).
/// </summary>
public sealed class ServiceTokenProvider(IHttpClientFactory factory, IConfiguration config, ILogger<ServiceTokenProvider> log)
{
    private readonly string _email = config["AgentTools:ServiceAccount:Email"] ?? "bob@duckstore.test";
    private readonly string _password = config["AgentTools:ServiceAccount:Password"] ?? "letmein";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expires = DateTimeOffset.MinValue;

    public async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        // Refresh a minute before expiry so a long chat turn doesn't hit an expired token mid-call.
        if (_token is not null && DateTimeOffset.UtcNow < _expires.AddMinutes(-1))
        {
            return _token;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expires.AddMinutes(-1))
            {
                return _token;
            }

            var client = factory.CreateClient("store-login");
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(_email, _password), ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Service-account login failed ({Status}); falling back to no token.", (int)response.StatusCode);
                return _token = null;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResult>(ct);
            _token = result?.Token;
            _expires = result?.ExpiresAt ?? DateTimeOffset.UtcNow;
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}

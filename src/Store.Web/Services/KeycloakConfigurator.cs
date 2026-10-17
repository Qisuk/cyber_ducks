using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberDucks.Shared.Defences;

namespace Store.Web.Services;

/// <summary>
/// VULN-OAUTH-02: sets the Keycloak client's redirect-URI policy at startup (restart-required).
/// Vulnerable registers a wildcard redirect URI, so Keycloak will send the auth code to any path on
/// the host — an attacker-controlled page can capture it (threat CT18). Secure registers only the
/// exact callback. Redirect-URI validation lives in the IdP, so this reconfigures Keycloak directly.
/// </summary>
public sealed class KeycloakConfigurator(
    IHttpClientFactory clients, IConfiguration config, ILogger<KeycloakConfigurator> log) : IHostedService
{
    private static readonly (string Realm, string Scheme)[] Realms = [("google", "google"), ("meta", "meta")];

    public async Task StartAsync(CancellationToken ct)
    {
        var secure = await StartupFlags.IsSecureAsync(config, "VULN-OAUTH-02");
        var baseUrl = $"http://{config["Oidc:InternalHost"] ?? "idp:8080"}";
        var admin = config["Keycloak:AdminUser"] ?? "admin";
        var password = config["Keycloak:AdminPassword"];
        if (string.IsNullOrEmpty(password))
        {
            log.LogWarning("Keycloak admin password not set; skipping redirect-URI configuration.");
            return;
        }

        try
        {
            var http = clients.CreateClient();
            http.BaseAddress = new Uri(baseUrl);
            var token = await GetAdminTokenAsync(http, admin, password, ct);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            foreach (var (realm, scheme) in Realms)
            {
                string[] redirectUris = secure
                    ? [$"http://localhost:8080/signin-{scheme}"]
                    : ["http://localhost:8080/*"]; // loose: any path on the host
                await SetRedirectUrisAsync(http, realm, redirectUris, ct);
            }
            log.LogInformation("Keycloak redirect-URI policy set to {Mode}", secure ? "secure (exact)" : "vulnerable (wildcard)");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not configure Keycloak redirect URIs (VULN-OAUTH-02).");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task<string> GetAdminTokenAsync(HttpClient http, string user, string password, CancellationToken ct)
    {
        using var response = await http.PostAsync("/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = user,
                ["password"] = password,
            }), ct);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("access_token").GetString()!;
    }

    private static async Task SetRedirectUrisAsync(HttpClient http, string realm, string[] redirectUris, CancellationToken ct)
    {
        var clients = await http.GetFromJsonAsync<JsonElement[]>(
            $"/admin/realms/{realm}/clients?clientId=cyber-ducks-web", ct);
        var clientId = clients![0].GetProperty("id").GetString();

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/admin/realms/{realm}/clients/{clientId}")
        {
            Content = JsonContent.Create(new { redirectUris }),
        };
        (await http.SendAsync(update, ct)).EnsureSuccessStatusCode();
    }
}

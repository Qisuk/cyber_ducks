using System.Net;
using CyberDucks.Shared.Contracts;

namespace Store.Web.Services;

/// <summary>Typed client for store-api (flow F4).</summary>
public sealed class StoreApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ProductSummary>> GetProductsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProductSummary>>("api/products", ct) ?? [];

    /// <summary>Returns null when the product doesn't exist.</summary>
    public async Task<ProductDetail?> GetProductAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/products/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDetail>(ct);
    }

    public Task<PingResponse?> PingAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<PingResponse>("api/ping", ct);

    /// <summary>The current user from store-api, using the forwarded JWT.</summary>
    public Task<UserInfo?> GetMeAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<UserInfo>("api/auth/me", ct);

    /// <summary>Attempts login. Returns the auth result, or an error message to show the user.</summary>
    public Task<(AuthResult? Result, string? Error)> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        PostAuthAsync("api/auth/login", request, ct);

    public Task<(AuthResult? Result, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        PostAuthAsync("api/auth/register", request, ct);

    private async Task<(AuthResult?, string?)> PostAuthAsync(string path, object request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<AuthResult>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
        return (null, problem?.Message ?? "Something went wrong. Please try again.");
    }

    public sealed record PingResponse(string Service, DateTime DatabaseTime);

    private sealed record ErrorResponse(string? Message);
}

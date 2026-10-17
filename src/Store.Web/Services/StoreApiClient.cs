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

    public sealed record PingResponse(string Service, DateTime DatabaseTime);
}

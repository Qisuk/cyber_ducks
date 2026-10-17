using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;

namespace AgentTools;

/// <summary>Typed access to store-api for the tools. Calls carry the customer's token (flow F22).</summary>
public sealed class StoreApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<OrderSummary>> GetMyOrdersAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<IReadOnlyList<OrderSummary>>("/api/orders", ct) ?? [];

    public async Task<OrderDetail?> GetOrderAsync(int id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/orders/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDetail>(ct);
    }
}

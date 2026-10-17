using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;
using Npgsql;

namespace CyberDucks.Tests;

/// <summary>VULN-LOGIC-02 (cancel after ship) and its database-guard second layer.</summary>
public sealed class OrderCancelTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Client, int OrderId)> PlaceOrderAsync(string status = "placed")
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"buyer-{Guid.NewGuid():N}@example.test", "Buyer", "Correct-Horse-1"), Ct);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var place = await client.PostAsJsonAsync("/api/orders",
            new PlaceOrderRequest("1 Test St", null, [new PlaceOrderItem(1, 1, 4.99m)]), Ct);
        var order = await place.Content.ReadFromJsonAsync<PlaceOrderResult>(Ct);

        if (status != "placed")
        {
            await SetOrderStatusAsync(order!.OrderId, status);
        }
        return (client, order!.OrderId);
    }

    private async Task SetOrderStatusAsync(int orderId, string status)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString("store_owner", "duckstore"));
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("UPDATE orders SET status = @s WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("s", status);
        cmd.Parameters.AddWithValue("id", orderId);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    private async Task SetGuardAsync(string guard, bool enabled)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString("range_control_owner", "duckstore"));
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("SELECT set_guard(@g, @e)", conn);
        cmd.Parameters.AddWithValue("g", guard);
        cmd.Parameters.AddWithValue("e", enabled);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    private async Task<string> StatusOfAsync(int orderId)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString("store_owner", "duckstore"));
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("SELECT status FROM orders WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", orderId);
        return (string)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task Secure_blocks_cancelling_a_shipped_order()
    {
        _factory.SetFlag("VULN-LOGIC-02", DefenceMode.Secure);
        var (client, orderId) = await PlaceOrderAsync("shipped");

        var response = await client.PostAsync($"/api/orders/{orderId}/cancel", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("shipped", await StatusOfAsync(orderId));
    }

    [Fact]
    public async Task Secure_allows_cancelling_a_placed_order()
    {
        _factory.SetFlag("VULN-LOGIC-02", DefenceMode.Secure);
        var (client, orderId) = await PlaceOrderAsync();

        var response = await client.PostAsync($"/api/orders/{orderId}/cancel", null, Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("cancelled", await StatusOfAsync(orderId));
    }

    [Fact]
    public async Task Vulnerable_cancels_a_shipped_order_the_ui_would_never_offer()
    {
        _factory.SetFlag("VULN-LOGIC-02", DefenceMode.Vulnerable);
        await SetGuardAsync("orders_no_cancel_after_ship", enabled: false);
        var (client, orderId) = await PlaceOrderAsync("shipped");

        var response = await client.PostAsync($"/api/orders/{orderId}/cancel", null, Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("cancelled", await StatusOfAsync(orderId));
    }

    [Fact]
    public async Task Database_guard_blocks_the_cancel_even_when_the_app_allows_it()
    {
        // Defence in depth: VULN-LOGIC-02 vulnerable, but the DB guard still refuses the transition.
        _factory.SetFlag("VULN-LOGIC-02", DefenceMode.Vulnerable);
        await SetGuardAsync("orders_no_cancel_after_ship", enabled: true);
        try
        {
            var (client, orderId) = await PlaceOrderAsync("shipped");

            var response = await client.PostAsync($"/api/orders/{orderId}/cancel", null, Ct);

            Assert.False(response.IsSuccessStatusCode);          // the DB raised an exception
            Assert.Equal("shipped", await StatusOfAsync(orderId)); // and nothing changed
        }
        finally
        {
            await SetGuardAsync("orders_no_cancel_after_ship", enabled: false);
        }
    }

    public void Dispose() => _factory.Dispose();
}

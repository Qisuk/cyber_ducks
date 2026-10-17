using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

/// <summary>VULN-IDOR-01 on GET /api/orders/{id}. Seed: order 1001 is Alice's (user 1), 1004 is Bob's (user 2).</summary>
public sealed class OrderIdorTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> ClientForAsync(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _factory.TokenForAsync(userId));
        return client;
    }

    [Fact]
    public async Task Vulnerable_lets_a_customer_read_another_customers_order()
    {
        _factory.SetFlag("VULN-IDOR-01", DefenceMode.Vulnerable);
        var alice = await ClientForAsync(1);

        // Alice fetches Bob's order 1004 just by changing the id.
        var order = await alice.GetFromJsonAsync<OrderDetail>("/api/orders/1004", Ct);
        Assert.NotNull(order);
        Assert.Equal(1004, order.Id);
    }

    [Fact]
    public async Task Secure_blocks_reading_another_customers_order()
    {
        _factory.SetFlag("VULN-IDOR-01", DefenceMode.Secure);
        var alice = await ClientForAsync(1);

        var others = await alice.GetAsync("/api/orders/1004", Ct);
        Assert.Equal(HttpStatusCode.NotFound, others.StatusCode);

        // Alice can still see her own order.
        var own = await alice.GetAsync("/api/orders/1001", Ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Listing_only_returns_the_callers_own_orders()
    {
        var alice = await ClientForAsync(1);
        var orders = await alice.GetFromJsonAsync<List<OrderSummary>>("/api/orders", Ct);

        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        // Alice has orders 1001-1003; none of Bob's.
        Assert.All(orders, o => Assert.InRange(o.Id, 1001, 1003));
    }

    [Fact]
    public async Task Orders_require_authentication()
    {
        var anon = await _factory.CreateClient().GetAsync("/api/orders/1001", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}

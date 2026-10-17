using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

/// <summary>Placing an order, and VULN-LOGIC-01 (client price tampering) on checkout.</summary>
public sealed class OrderPlacementTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // A fresh customer per test, so placed orders never disturb the seeded personas' order lists.
    private async Task<HttpClient> NewCustomerAsync()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"buyer-{Guid.NewGuid():N}@example.test", "Buyer", "Correct-Horse-1"), Ct);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    // Golden Duck (product 12) really costs £49.99; the request claims 1p.
    private static PlaceOrderRequest TamperedGoldenDuck() =>
        new("1 Test Street", null, [new PlaceOrderItem(12, 1, 0.01m)]);

    [Fact]
    public async Task Vulnerable_checkout_trusts_the_client_price()
    {
        _factory.SetFlag("VULN-LOGIC-01", DefenceMode.Vulnerable);
        var client = await NewCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/orders", TamperedGoldenDuck(), Ct);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResult>(Ct);

        Assert.Equal(0.01m, result!.Total); // bought a £49.99 duck for 1p
    }

    [Fact]
    public async Task Secure_checkout_recomputes_from_the_catalogue()
    {
        _factory.SetFlag("VULN-LOGIC-01", DefenceMode.Secure);
        var client = await NewCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/orders", TamperedGoldenDuck(), Ct);
        var result = await response.Content.ReadFromJsonAsync<PlaceOrderResult>(Ct);

        Assert.Equal(49.99m, result!.Total); // the tampered price is ignored
    }

    [Fact]
    public async Task A_placed_order_appears_in_the_customers_list_with_an_open_invoice()
    {
        _factory.SetFlag("VULN-LOGIC-01", DefenceMode.Secure);
        var client = await NewCustomerAsync();

        var place = await client.PostAsJsonAsync("/api/orders",
            new PlaceOrderRequest("22 Quack Street", "Leave at door", [new PlaceOrderItem(1, 2, 4.99m)]), Ct);
        var created = await place.Content.ReadFromJsonAsync<PlaceOrderResult>(Ct);

        var detail = await client.GetFromJsonAsync<OrderDetail>($"/api/orders/{created!.OrderId}", Ct);
        Assert.Equal("placed", detail!.Status);
        Assert.Equal(9.98m, detail.Total);
        Assert.Equal("open", detail.Invoice!.Status);
    }

    public void Dispose() => _factory.Dispose();
}

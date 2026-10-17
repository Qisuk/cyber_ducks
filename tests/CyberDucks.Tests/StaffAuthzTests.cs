using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

/// <summary>VULN-AUTHZ-01: function-level authorisation on the staff endpoints. Eve (user 5) is an employee.</summary>
public sealed class StaffAuthzTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> CustomerAsync()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"cust-{Guid.NewGuid():N}@example.test", "Cust", "Correct-Horse-1"), Ct);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private async Task<HttpClient> StaffAsync()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _factory.TokenForAsync(5));
        return client;
    }

    [Fact]
    public async Task Vulnerable_lets_a_customer_see_the_fulfilment_queue()
    {
        _factory.SetFlag("VULN-AUTHZ-01", DefenceMode.Vulnerable);
        var customer = await CustomerAsync();

        var response = await customer.GetAsync("/api/staff/orders", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Secure_forbids_a_customer_from_staff_endpoints()
    {
        _factory.SetFlag("VULN-AUTHZ-01", DefenceMode.Secure);
        var customer = await CustomerAsync();

        var list = await customer.GetAsync("/api/staff/orders", Ct);
        var ship = await customer.PostAsync("/api/staff/orders/1001/ship", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ship.StatusCode);
    }

    [Fact]
    public async Task Secure_allows_a_staff_member()
    {
        _factory.SetFlag("VULN-AUTHZ-01", DefenceMode.Secure);
        var staff = await StaffAsync();

        var response = await staff.GetAsync("/api/staff/orders", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_unauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/staff/orders", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}

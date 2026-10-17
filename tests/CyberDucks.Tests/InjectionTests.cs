using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

/// <summary>VULN-SQLI-01 (product search) and VULN-INFO-01 (verbose errors), which pair up.</summary>
public sealed class InjectionTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // UNION injection that surfaces every user's email in the product "name" column.
    private const string EmailDump = "' UNION SELECT id, 'x', email, 0, 0, null FROM users -- ";

    [Fact]
    public async Task Vulnerable_search_allows_union_injection_to_leak_user_emails()
    {
        _factory.SetFlag("VULN-SQLI-01", DefenceMode.Vulnerable);
        var client = _factory.CreateClient();

        var results = await client.GetFromJsonAsync<List<ProductSummary>>(
            $"/api/products/search?q={Uri.EscapeDataString(EmailDump)}", Ct);

        Assert.NotNull(results);
        Assert.Contains(results, r => r.Name == "alice@duckstore.test");
    }

    [Fact]
    public async Task Secure_search_treats_the_payload_as_a_literal_string()
    {
        _factory.SetFlag("VULN-SQLI-01", DefenceMode.Secure);
        var client = _factory.CreateClient();

        var results = await client.GetFromJsonAsync<List<ProductSummary>>(
            $"/api/products/search?q={Uri.EscapeDataString(EmailDump)}", Ct);

        // No product is literally named that, and no injection happens: zero rows.
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Secure_search_still_finds_matching_products()
    {
        _factory.SetFlag("VULN-SQLI-01", DefenceMode.Secure);
        var client = _factory.CreateClient();

        var results = await client.GetFromJsonAsync<List<ProductSummary>>("/api/products/search?q=pirate", Ct);
        Assert.Contains(results!, r => r.Sku == "DUCK-PIRATE");
    }

    [Fact]
    public async Task Vulnerable_errors_leak_a_stack_trace()
    {
        // A lone quote breaks the concatenated SQL, forcing a server error.
        _factory.SetFlag("VULN-SQLI-01", DefenceMode.Vulnerable);
        _factory.SetFlag("VULN-INFO-01", DefenceMode.Vulnerable);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/search?q=%27", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.Contains("stackTrace", body);
        Assert.Contains("Npgsql", body); // reveals the data layer
    }

    [Fact]
    public async Task Secure_errors_are_generic()
    {
        _factory.SetFlag("VULN-SQLI-01", DefenceMode.Vulnerable);
        _factory.SetFlag("VULN-INFO-01", DefenceMode.Secure);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/search?q=%27", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(500, (int)response.StatusCode);
        Assert.DoesNotContain("stackTrace", body);
        Assert.Contains("unexpected error", body);
    }

    public void Dispose() => _factory.Dispose();
}

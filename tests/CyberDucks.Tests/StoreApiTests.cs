using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CyberDucks.Tests;

public sealed class StoreApiTests(PostgresFixture db) : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(host => host
            .UseSetting("ConnectionStrings:Store", db.ConnectionString("store_owner", "duckstore"))
            .UseSetting("ConnectionStrings:Flags", db.ConnectionString("flags_reader", "range_control"))
            .UseSetting("Jwt:Secret", "test-signing-secret-at-least-32-bytes-long!!"));

    [Fact]
    public async Task Ready_when_database_reachable()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ping_returns_database_time()
    {
        var ping = await _factory.CreateClient()
            .GetFromJsonAsync<PingResponse>("/api/ping", TestContext.Current.CancellationToken);
        Assert.NotNull(ping);
        Assert.Equal("store-api", ping.Service);
        Assert.True(ping.DatabaseTime > DateTime.UtcNow.AddMinutes(-5));
    }

    public void Dispose() => _factory.Dispose();

    private sealed record PingResponse(string Service, DateTime DatabaseTime);
}

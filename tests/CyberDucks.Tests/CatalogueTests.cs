using System.Net;
using System.Net.Http.Json;
using CyberDucks.Shared.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CyberDucks.Tests;

/// <summary>Catalogue API against the seeded database (infra/postgres/init/05-seed.sql).</summary>
public sealed class CatalogueTests(PostgresFixture db) : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(host => host
            .UseSetting("ConnectionStrings:Store", db.ConnectionString("store_owner", "duckstore"))
            .UseSetting("ConnectionStrings:Flags", db.ConnectionString("flags_reader", "range_control"))
            .UseSetting("Jwt:Secret", "test-signing-secret-at-least-32-bytes-long!!"));

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lists_all_twelve_ducks_in_id_order()
    {
        var ducks = await _factory.CreateClient().GetFromJsonAsync<List<ProductSummary>>("/api/products", Ct);

        Assert.NotNull(ducks);
        Assert.Equal(12, ducks.Count);
        Assert.Equal(Enumerable.Range(1, 12), ducks.Select(d => d.Id));
        Assert.Equal("Classic Yellow Duck", ducks[0].Name);
        Assert.Equal(4.99m, ducks[0].Price);
    }

    [Fact]
    public async Task Summary_includes_review_stats()
    {
        var ducks = await _factory.CreateClient().GetFromJsonAsync<List<ProductSummary>>("/api/products", Ct);

        var debugger = Assert.Single(ducks!, d => d.Sku == "DUCK-DEBUG");
        Assert.Equal(1, debugger.ReviewCount);
        Assert.Equal(5, debugger.AverageRating);

        var pirate = Assert.Single(ducks!, d => d.Sku == "DUCK-PIRATE");
        Assert.Equal(0, pirate.ReviewCount);
        Assert.Null(pirate.AverageRating);
    }

    [Fact]
    public async Task Detail_includes_reviews_with_author_names()
    {
        var duck = await _factory.CreateClient().GetFromJsonAsync<ProductDetail>("/api/products/2", Ct);

        Assert.NotNull(duck);
        Assert.Equal("Debugger Duck", duck.Name);
        var review = Assert.Single(duck.Reviews);
        Assert.Equal("Alice Anders", review.Author);
        Assert.Equal(5, review.Rating);
    }

    [Fact]
    public async Task Unknown_product_is_404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/products/999", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}

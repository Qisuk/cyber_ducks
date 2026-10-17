using CyberDucks.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Catalogue;

/// <summary>Public, read-only product catalogue. No authentication required.</summary>
public static class CatalogueEndpoints
{
    public static IEndpointRouteBuilder MapCatalogue(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Catalogue");

        products.MapGet("/", async (StoreDbContext db, CancellationToken ct) =>
            await db.Products
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new ProductSummary(
                    p.Id, p.Sku, p.Name, p.Price, p.Stock, p.ImageUrl,
                    p.Reviews.Average(r => (double?)r.Rating),
                    p.Reviews.Count))
                .ToListAsync(ct));

        products.MapGet("/{id:int}", async Task<IResult> (int id, StoreDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new ProductDetail(
                    p.Id, p.Sku, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl,
                    p.Reviews.Average(r => (double?)r.Rating),
                    p.Reviews
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => new ReviewView(r.Id, r.User!.DisplayName, r.Rating, r.Body, r.CreatedAt))
                        .ToList()))
                .SingleOrDefaultAsync(ct);

            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        return app;
    }
}

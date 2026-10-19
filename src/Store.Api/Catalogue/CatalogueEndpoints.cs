using System.Security.Claims;
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

        // Public product search. The query handling is behind VULN-SQLI-01.
        products.MapGet("/search", async (string? q, IProductSearch search, CancellationToken ct) =>
            await search.SearchAsync(q ?? "", ct));

        products.MapGet("/", async (StoreDbContext db, CancellationToken ct) =>
            await db.Products
                .AsNoTracking()
                .Where(p => p.Visible)   // CTF hidden-duck: a non-visible duck is reachable only by /{id} enumeration
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

        // Posting a review. The body is stored as-is: the stored-XSS weakness (VULN-XSS-01) is in
        // how store-web renders it, not how it's stored. Reviews are also the indirect
        // prompt-injection carrier for the AI agent (threat TH8).
        products.MapPost("/{id:int}/reviews", async Task<IResult> (
            int id, PostReviewRequest request, ClaimsPrincipal principal,
            StoreDbContext db, CancellationToken ct) =>
        {
            if (!await db.Products.AnyAsync(p => p.Id == id, ct))
            {
                return Results.NotFound();
            }
            if (request.Rating is < 1 or > 5)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["rating"] = ["Rating must be between 1 and 5."],
                });
            }

            var userId = int.Parse(principal.FindFirstValue("sub")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var review = new Data.ProductReview
            {
                ProductId = id,
                UserId = userId,
                Rating = (short)request.Rating,
                Body = request.Body,
            };
            db.ProductReviews.Add(review);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/products/{id}", new { review.Id });
        }).RequireAuthorization();

        return app;
    }
}

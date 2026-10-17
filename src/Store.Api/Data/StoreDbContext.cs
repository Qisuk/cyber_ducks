using Microsoft.EntityFrameworkCore;

namespace Store.Api.Data;

/// <summary>
/// EF Core view of duckstore. Table and column names come from the snake_case naming convention
/// (ProductReviews -> product_reviews, DisplayName -> display_name), configured in Program.cs.
/// </summary>
public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
}

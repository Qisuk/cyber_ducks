namespace Store.Api.Data;

// EF Core entities over the schema in infra/postgres/init/02-schema.sql. The SQL scripts own the
// schema (ADR-0002): there are no EF migrations. Property names map to snake_case columns.

public sealed class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
    public string? PasswordHashLegacy { get; set; }
    public string? PasswordHash { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public List<ProductReview> Reviews { get; set; } = [];
}

public sealed class ProductReview
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public short Rating { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Product? Product { get; set; }
    public User? User { get; set; }
}

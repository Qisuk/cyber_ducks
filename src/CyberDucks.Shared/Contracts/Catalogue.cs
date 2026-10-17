namespace CyberDucks.Shared.Contracts;

// API contracts shared by store-api and its clients (store-web, later agent-tools).

public sealed record ProductSummary(
    int Id,
    string Sku,
    string Name,
    decimal Price,
    int Stock,
    string? ImageUrl,
    double? AverageRating,
    int ReviewCount);

public sealed record ProductDetail(
    int Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    double? AverageRating,
    IReadOnlyList<ReviewView> Reviews);

/// <summary>A review as shown to shoppers. <see cref="Body"/> and <see cref="Author"/> are user-supplied (✎).</summary>
public sealed record ReviewView(int Id, string Author, int Rating, string Body, DateTimeOffset CreatedAt);

/// <summary>A review a customer posts. <see cref="Body"/> is rendered back to other shoppers (VULN-XSS-01).</summary>
public sealed record PostReviewRequest(int Rating, string Body);

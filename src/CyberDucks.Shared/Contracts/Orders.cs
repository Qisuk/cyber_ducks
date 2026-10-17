namespace CyberDucks.Shared.Contracts;

// Order contracts shared by store-api and its clients.

public sealed record OrderSummary(
    int Id,
    string Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    int ItemCount,
    InvoiceView? Invoice);

public sealed record OrderDetail(
    int Id,
    string Status,
    string Notes,
    string ShippingAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ShippedAt,
    decimal Total,
    IReadOnlyList<OrderLine> Items,
    InvoiceView? Invoice);

public sealed record OrderLine(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public sealed record InvoiceView(int Id, decimal AmountDue, string Status, DateOnly DueDate, DateTimeOffset? PaidAt);

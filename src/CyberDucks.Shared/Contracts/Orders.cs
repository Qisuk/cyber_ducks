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

/// <summary>
/// A checkout request. Each line carries a client-supplied <see cref="PlaceOrderItem.UnitPrice"/>;
/// whether the server trusts it or recomputes from the catalogue is VULN-LOGIC-01.
/// </summary>
public sealed record PlaceOrderRequest(string ShippingAddress, string? Notes, IReadOnlyList<PlaceOrderItem> Items);

public sealed record PlaceOrderItem(int ProductId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderResult(int OrderId, decimal Total);

/// <summary>An order as seen on the staff fulfilment queue (includes the customer's email).</summary>
public sealed record StaffOrderSummary(
    int Id, string CustomerEmail, string Status, decimal Total, DateTimeOffset CreatedAt, string ShippingAddress);

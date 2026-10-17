using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentTools;

/// <summary>
/// The allow-listed tools the agent may call (ADR-0009). Each one is scoped to the current
/// customer by store-api, because <see cref="StoreApiClient"/> forwards that customer's token.
/// Descriptions state plainly what the tool does and never carry instructions to the model.
/// </summary>
[McpServerToolType]
public sealed class OrderTools
{
    [McpServerTool(Name = "get_my_orders")]
    [Description("List the signed-in customer's own orders: id, status, total, when placed, number of items, and invoice status. Use this to answer questions about the customer's order history.")]
    public static async Task<IReadOnlyList<OrderBrief>> GetMyOrders(StoreApiClient store, CancellationToken ct)
    {
        var orders = await store.GetMyOrdersAsync(ct);
        return orders.Select(o => new OrderBrief(
            o.Id, o.Status, o.Total, o.CreatedAt, o.ItemCount, o.Invoice?.Status)).ToList();
    }

    [McpServerTool(Name = "get_order_status")]
    [Description("Get the current status and dates for one of the customer's orders by its id. Returns not-found if the order does not belong to the customer.")]
    public static async Task<OrderStatus?> GetOrderStatus(
        StoreApiClient store,
        [Description("The order id, as shown by get_my_orders.")] int orderId,
        CancellationToken ct)
    {
        var order = await store.GetOrderAsync(orderId, ct);
        return order is null ? null
            : new OrderStatus(order.Id, order.Status, order.CreatedAt, order.ShippedAt, order.Total);
    }
}

/// <summary>Trimmed order view for the agent: only what answering order questions needs.</summary>
public sealed record OrderBrief(
    int Id, string Status, decimal Total, DateTimeOffset CreatedAt, int ItemCount, string? InvoiceStatus);

public sealed record OrderStatus(
    int Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ShippedAt, decimal Total);

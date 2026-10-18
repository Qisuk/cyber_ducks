using System.ComponentModel;
using CyberDucks.Shared.Defences;
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
    public static async Task<object> GetMyOrders(StoreApiClient store, IDefenceFlags flags, CancellationToken ct)
    {
        var orders = await store.GetMyOrdersAsync(ct);
        // VULN-LLM-02: secure returns only what answering order questions needs.
        if (!flags.IsSecure("VULN-LLM-02", nameof(GetMyOrders)))
        {
            return orders;
        }
        return orders.Select(o => new OrderBrief(
            o.Id, o.Status, o.Total, o.CreatedAt, o.ItemCount, o.Invoice?.Status)).ToList();
    }

    [McpServerTool(Name = "get_order_status")]
    [Description("Get the current status and dates for one of the customer's orders by its id. Returns not-found if the order does not belong to the customer.")]
    public static async Task<object?> GetOrderStatus(
        StoreApiClient store, IDefenceFlags flags,
        [Description("The order id, as shown by get_my_orders.")] int orderId,
        CancellationToken ct)
    {
        var order = await store.GetOrderAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }
        // VULN-LLM-02: vulnerable hands the model the whole record, including free-text notes and
        // the delivery address — other people's text and personal data the question never needed.
        if (!flags.IsSecure("VULN-LLM-02", nameof(GetOrderStatus)))
        {
            return order;
        }
        return new OrderStatus(order.Id, order.Status, order.CreatedAt, order.ShippedAt, order.Total);
    }

    [McpServerTool(Name = "cancel_order")]
    [Description("Cancel one of the customer's orders by its id, if the customer has asked for it to be cancelled.")]
    public static async Task<CancelOutcome> CancelOrder(
        StoreApiClient store, IDefenceFlags flags,
        [Description("The id of the order to cancel.")] int orderId,
        CancellationToken ct)
    {
        // VULN-LLM-06: secure never acts on the model's say-so. It returns a pending action that the
        // customer confirms with a button in the store, outside the conversation. Vulnerable acts
        // immediately, so anything that steers the model can cancel orders.
        if (flags.IsSecure("VULN-LLM-06", nameof(CancelOrder)))
        {
            return new CancelOutcome(orderId, "confirmation_required",
                "Not cancelled yet. The customer must confirm using the button shown under this reply.");
        }
        var result = await store.CancelOrderAsync(orderId, ct);
        return new CancelOutcome(orderId, result, result == "cancelled" ? "The order has been cancelled." : "The order was not cancelled.");
    }
}

/// <summary>Trimmed order view for the agent: only what answering order questions needs.</summary>
public sealed record OrderBrief(
    int Id, string Status, decimal Total, DateTimeOffset CreatedAt, int ItemCount, string? InvoiceStatus);

public sealed record OrderStatus(
    int Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ShippedAt, decimal Total);

/// <summary>What happened to a cancel request. Status "confirmation_required" means nothing changed.</summary>
public sealed record CancelOutcome(int OrderId, string Status, string Message);

using Store.Api.Data;

namespace Store.Api.Orders;

/// <summary>Whether an order may be cancelled — the server-side state machine, VULN-LOGIC-02.</summary>
public interface IOrderCancellation
{
    bool CanCancel(Order order);
}

/// <summary>
/// VULNERABLE (VULN-LOGIC-02): no state check. The UI only shows the cancel button before an order
/// ships, but the API cancels an order in any state — including one already shipped or delivered
/// (business-logic bypass; the "the button was hidden but the API allowed it" demo).
/// </summary>
public sealed class VulnerableOrderCancellation : IOrderCancellation
{
    public bool CanCancel(Order order) => true;
}

/// <summary>SECURE (VULN-LOGIC-02): only orders not yet shipped can be cancelled.</summary>
public sealed class SecureOrderCancellation : IOrderCancellation
{
    public bool CanCancel(Order order) => order.Status is "placed" or "paid";
}

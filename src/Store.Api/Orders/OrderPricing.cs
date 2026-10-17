using CyberDucks.Shared.Contracts;
using Store.Api.Data;

namespace Store.Api.Orders;

/// <summary>
/// Decides the unit price stored for each order line — VULN-LOGIC-01. The catalogue price for each
/// product is passed in (looked up once by the endpoint).
/// </summary>
public interface IOrderPricing
{
    decimal UnitPriceFor(PlaceOrderItem item, IReadOnlyDictionary<int, decimal> cataloguePrices);
}

/// <summary>
/// VULNERABLE (VULN-LOGIC-01): trusts the price the client submitted. A shopper can buy a £49.99
/// duck for 1p (threat TH2, assets A13/A14) by tampering with the checkout request.
/// </summary>
public sealed class VulnerableOrderPricing : IOrderPricing
{
    public decimal UnitPriceFor(PlaceOrderItem item, IReadOnlyDictionary<int, decimal> cataloguePrices) =>
        item.UnitPrice;
}

/// <summary>SECURE (VULN-LOGIC-01): ignores the client price and uses the current catalogue price.</summary>
public sealed class SecureOrderPricing : IOrderPricing
{
    public decimal UnitPriceFor(PlaceOrderItem item, IReadOnlyDictionary<int, decimal> cataloguePrices) =>
        cataloguePrices[item.ProductId];
}

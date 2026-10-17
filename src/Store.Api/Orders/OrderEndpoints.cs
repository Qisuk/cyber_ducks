using System.Security.Claims;
using CyberDucks.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

        // The caller's own orders. Listing is always scoped to the current user.
        orders.MapGet("/", async (ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            var userId = CurrentUserId(principal);
            var list = await db.Orders.AsNoTracking()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderSummary(
                    o.Id,
                    o.Status,
                    o.Items.Sum(i => i.Quantity * i.UnitPrice),
                    o.CreatedAt,
                    o.Items.Count,
                    o.Invoice == null ? null : new InvoiceView(
                        o.Invoice.Id, o.Invoice.AmountDue, o.Invoice.Status, o.Invoice.DueDate, o.Invoice.PaidAt)))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        // A single order. Object-level authorisation is behind VULN-IDOR-01 (IOrderAccess).
        orders.MapGet("/{id:int}", async Task<IResult> (
            int id, ClaimsPrincipal principal, IOrderAccess access, CancellationToken ct) =>
        {
            var order = await access.FindAsync(id, CurrentUserId(principal), ct);
            return order is null ? Results.NotFound() : Results.Ok(ToDetail(order));
        });

        // Place an order. Line pricing is behind VULN-LOGIC-01 (IOrderPricing).
        orders.MapPost("/", PlaceOrderAsync);

        return app;
    }

    private static async Task<IResult> PlaceOrderAsync(
        PlaceOrderRequest request, ClaimsPrincipal principal, StoreDbContext db,
        IOrderPricing pricing, CancellationToken ct)
    {
        if (request.Items.Count == 0 || string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = ["An order needs a shipping address and at least one item."],
            });
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (productIds.Any(id => !products.ContainsKey(id)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = ["One or more products do not exist."],
            });
        }
        var cataloguePrices = products.ToDictionary(p => p.Key, p => p.Value.Price);

        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            UserId = CurrentUserId(principal),
            Status = "placed",
            Notes = request.Notes ?? "",
            ShippingAddress = request.ShippingAddress,
            CreatedAt = now,
            UpdatedAt = now,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = pricing.UnitPriceFor(i, cataloguePrices), // VULN-LOGIC-01
            }).ToList(),
        };

        var total = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        order.Invoice = new Invoice
        {
            AmountDue = total,
            Status = "open",
            DueDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(14),
        };

        foreach (var item in request.Items)
        {
            products[item.ProductId].Stock -= item.Quantity;
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct); // a DB guard (e.g. positive quantity) rejects a bad line here
        return Results.Ok(new PlaceOrderResult(order.Id, total));
    }

    internal static int CurrentUserId(ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static OrderDetail ToDetail(Order o) => new(
        o.Id, o.Status, o.Notes, o.ShippingAddress, o.CreatedAt, o.ShippedAt,
        o.Items.Sum(i => i.Quantity * i.UnitPrice),
        o.Items.Select(i => new OrderLine(i.ProductId, i.Product?.Name ?? $"#{i.ProductId}", i.Quantity, i.UnitPrice)).ToList(),
        o.Invoice is null ? null : new InvoiceView(o.Invoice.Id, o.Invoice.AmountDue, o.Invoice.Status, o.Invoice.DueDate, o.Invoice.PaidAt));
}

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

        return app;
    }

    internal static int CurrentUserId(ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static OrderDetail ToDetail(Order o) => new(
        o.Id, o.Status, o.Notes, o.ShippingAddress, o.CreatedAt, o.ShippedAt,
        o.Items.Sum(i => i.Quantity * i.UnitPrice),
        o.Items.Select(i => new OrderLine(i.ProductId, i.Product?.Name ?? $"#{i.ProductId}", i.Quantity, i.UnitPrice)).ToList(),
        o.Invoice is null ? null : new InvoiceView(o.Invoice.Id, o.Invoice.AmountDue, o.Invoice.Status, o.Invoice.DueDate, o.Invoice.PaidAt));
}

using System.Security.Claims;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Ctf;
using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Staff;

public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaff(this IEndpointRouteBuilder app)
    {
        // All endpoints require login; the staff-role check itself is VULN-AUTHZ-01 (IStaffGuard).
        var staff = app.MapGroup("/api/staff").WithTags("Staff").RequireAuthorization();

        // CTF flag `authz-api`: a staff-only report. The guard is VULN-AUTHZ-01 — in vulnerable mode
        // any signed-in customer passes IsStaff and gets the flag (broken function-level authz).
        staff.MapGet("/report", async Task<IResult> (
            ClaimsPrincipal user, IStaffGuard guard, CtfFlagReader flags, CancellationToken ct) =>
            guard.IsStaff(user)
                ? Results.Ok(new { report = "fulfilment-internal", token = await flags.GetAsync("authz-api", ct) })
                : Results.Forbid());

        // The fulfilment queue: orders waiting to be shipped.
        staff.MapGet("/orders", async Task<IResult> (
            ClaimsPrincipal user, IStaffGuard guard, StoreDbContext db, CancellationToken ct) =>
        {
            if (!guard.IsStaff(user))
            {
                return Results.Forbid();
            }
            var orders = await db.Orders.AsNoTracking()
                .OrderBy(o => o.CreatedAt)
                .Select(o => new StaffOrderSummary(
                    o.Id, o.User!.Email, o.Status, o.Items.Sum(i => i.Quantity * i.UnitPrice),
                    o.CreatedAt, o.ShippingAddress))
                .ToListAsync(ct);
            return Results.Ok(orders);
        });

        // Mark an order shipped.
        staff.MapPost("/orders/{id:int}/ship", async Task<IResult> (
            int id, ClaimsPrincipal user, IStaffGuard guard, StoreDbContext db, CancellationToken ct) =>
        {
            if (!guard.IsStaff(user))
            {
                return Results.Forbid();
            }
            var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
            if (order is null)
            {
                return Results.NotFound();
            }
            if (order.Status is not ("placed" or "paid"))
            {
                return Results.Conflict(new { message = $"Order {id} cannot be shipped (status: {order.Status})." });
            }
            order.Status = "shipped";
            order.ShippedAt = DateTimeOffset.UtcNow;
            order.UpdatedAt = order.ShippedAt.Value;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { order.Id, order.Status });
        });

        return app;
    }
}

using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Orders;

/// <summary>
/// Fetches a single order with its items and invoice, applying (or, when vulnerable, skipping)
/// the object-level authorisation check — VULN-IDOR-01.
/// </summary>
public interface IOrderAccess
{
    /// <summary>The order the caller asked for, or null if it doesn't exist (or isn't theirs, when secure).</summary>
    Task<Order?> FindAsync(int orderId, int currentUserId, CancellationToken ct);
}

/// <summary>
/// VULNERABLE (VULN-IDOR-01): returns the order by id alone. Any logged-in customer can read any
/// other customer's order and invoice by changing the id (threat TH4, assets A1/A6).
/// </summary>
public sealed class VulnerableOrderAccess(StoreDbContext db) : IOrderAccess
{
    public Task<Order?> FindAsync(int orderId, int currentUserId, CancellationToken ct) =>
        OrderQuery.WithDetails(db).SingleOrDefaultAsync(o => o.Id == orderId, ct);
}

/// <summary>SECURE (VULN-IDOR-01): the order must belong to the current user.</summary>
public sealed class SecureOrderAccess(StoreDbContext db) : IOrderAccess
{
    public Task<Order?> FindAsync(int orderId, int currentUserId, CancellationToken ct) =>
        OrderQuery.WithDetails(db).SingleOrDefaultAsync(o => o.Id == orderId && o.UserId == currentUserId, ct);
}

internal static class OrderQuery
{
    public static IQueryable<Order> WithDetails(StoreDbContext db) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Invoice);
}

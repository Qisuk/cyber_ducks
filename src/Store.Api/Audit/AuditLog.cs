using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Audit;

/// <summary>
/// A durable, queryable trail of security-relevant actions (VULN-LOG-01). This is deliberately
/// separate from the OTel telemetry the rest of the range leans on: the audit trail must outlive
/// trace retention and answer the wargame's "when did we become aware?" question.
/// </summary>
public interface IAuditLog
{
    /// <summary>Record a security-relevant action. Safe to await inline on the request path.</summary>
    Task WriteAsync(string action, int? userId, string? objectType = null, string? objectId = null,
        object? details = null, CancellationToken ct = default);
}

/// <summary>
/// VULNERABLE (VULN-LOG-01): security events are not recorded, so after an incident there is no
/// reliable trail of who did what — detection and the after-action timeline suffer (A09:2021).
/// </summary>
public sealed class NoAuditLog : IAuditLog
{
    public Task WriteAsync(string action, int? userId, string? objectType = null, string? objectId = null,
        object? details = null, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// SECURE (VULN-LOG-01): write a structured <c>audit_log</c> row (with the current trace id) for
/// each security-relevant action. <c>store_app</c> has INSERT-only on the table, so the trail is
/// append-only for the app.
/// </summary>
public sealed class DbAuditLog(StoreDbContext db) : IAuditLog
{
    public async Task WriteAsync(string action, int? userId, string? objectType = null, string? objectId = null,
        object? details = null, CancellationToken ct = default)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var detailsJson = details is null ? "{}" : JsonSerializer.Serialize(details);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO audit_log (user_id, action, object_type, object_id, trace_id, details)
               VALUES ({userId}, {action}, {objectType}, {objectId}, {traceId}, CAST({detailsJson} AS jsonb))",
            ct);
    }
}

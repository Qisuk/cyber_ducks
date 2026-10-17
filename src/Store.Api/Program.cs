using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry;
using Store.Api.Catalogue;
using Store.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration);

// Store database. 'Store' connects as store_owner (vulnerable DB-role mode); 'StoreApp' as the
// least-privilege store_app. VULN-DBROLE-01 chooses between them (step 3d).
builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("Store")
        ?? throw new InvalidOperationException("ConnectionStrings:Store is not configured."));

// EF Core shares the same pooled data source; the SQL init scripts own the schema (no migrations).
builder.Services.AddDbContext<StoreDbContext>((sp, options) => options
    .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
    .UseSnakeCaseNamingConvention());

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// Liveness: the process is up. Readiness: it can reach the database.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// Round trip through the database, so a single request shows a web -> api -> postgres trace.
app.MapGet("/api/ping", async (NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("SELECT now()");
    var now = (DateTime)(await cmd.ExecuteScalarAsync())!;
    return Results.Ok(new { service = "store-api", databaseTime = now });
});

app.MapCatalogue();

app.Run();

internal sealed class DatabaseHealthCheck(NpgsqlDataSource db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        // Health probes run every few seconds; keep them out of traces.
        using var _ = SuppressInstrumentationScope.Begin();
        try
        {
            await using var cmd = db.CreateCommand("SELECT 1");
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", ex);
        }
    }
}

// Lets WebApplicationFactory<Program> host the API in tests.
public partial class Program;

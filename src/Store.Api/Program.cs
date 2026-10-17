using System.Text;
using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry;
using Store.Api;
using Store.Api.Auth;
using Store.Api.Catalogue;
using Store.Api.Orders;
using Store.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Store database. 'Store' connects as store_owner (vulnerable DB-role mode); 'StoreApp' as the
// least-privilege store_app. VULN-DBROLE-01 chooses between them (step 3d).
builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("Store")
        ?? throw new InvalidOperationException("ConnectionStrings:Store is not configured."));

// EF Core shares the same pooled data source; the SQL init scripts own the schema (no migrations).
builder.Services.AddDbContext<StoreDbContext>((sp, options) => options
    .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
    .UseSnakeCaseNamingConvention());

// --- Authentication (step 3c) ---
var jwtOptions = new JwtOptions
{
    Secret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured."),
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtIssuer>();
builder.Services.AddHostedService<PersonaPasswordSeeder>();

// The toggled defences (ADR-0011).
builder.Services.AddDefence<IPasswordVerifier, VulnerablePasswordVerifier, SecurePasswordVerifier>("VULN-PWD-01");
builder.Services.AddDefence<ILoginGuard, VulnerableLoginGuard, SecureLoginGuard>("VULN-AUTH-01");
builder.Services.AddDefence<IOrderAccess, VulnerableOrderAccess, SecureOrderAccess>("VULN-IDOR-01");
builder.Services.AddDefence<IProductSearch, VulnerableProductSearch, SecureProductSearch>("VULN-SQLI-01");
builder.Services.AddDefence<IProfileUpdate, VulnerableProfileUpdate, SecureProfileUpdate>("VULN-MASS-01");
builder.Services.AddDefence<IOrderPricing, VulnerableOrderPricing, SecureOrderPricing>("VULN-LOGIC-01");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep JWT claim names (sub, email, name) as-is
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            // Signature handling is taken over by TogglingSignatureValidator (VULN-SESSION-01), so the
            // framework's own signing-key / signed-token checks are turned off here.
            ValidateIssuerSigningKey = false,
            RequireSignedTokens = false,
            RoleClaimType = JwtIssuer.RoleClaim,
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
// VULN-SESSION-01: the signature check reads the live flag per token (vulnerable accepts forged/
// unsigned tokens; secure verifies the HMAC). Added as post-config so it can inject IDefenceFlags.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IDefenceFlags>((options, flags) =>
        options.TokenValidationParameters.SignatureValidator =
            (token, parameters) => TogglingSignatureValidator.Validate(token, parameters, flags));
builder.Services.AddAuthorization();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// First in the pipeline so it catches exceptions from everything below (VULN-INFO-01).
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

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
app.MapAuth();
app.MapOrders();

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

using System.Security.Claims;
using CyberDucks.Shared.Telemetry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using RangeControl;
using RangeControl.Components;
using RangeControl.Wargame;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Two connections as range_control_owner: flags live in range_control, guards in duckstore.
var controlDb = new NpgsqlDataSourceBuilder(
    builder.Configuration.GetConnectionString("RangeControl")
        ?? throw new InvalidOperationException("ConnectionStrings:RangeControl is not configured.")).Build();
var storeDb = new NpgsqlDataSourceBuilder(
    builder.Configuration.GetConnectionString("Duckstore")
        ?? throw new InvalidOperationException("ConnectionStrings:Duckstore is not configured.")).Build();
builder.Services.AddSingleton(new FlagAdminService(controlDb, storeDb));

// Wargame / Ops Console (ADR-0008): exercise state in range_control; scenarios and the topology
// are files mounted read-only into the container.
builder.Services.AddSingleton<WargameEvents>();
builder.Services.AddSingleton(sp => new WargameService(controlDb, sp.GetRequiredService<WargameEvents>()));
builder.Services.AddSingleton(new ScenarioCatalog(builder.Configuration["Wargame:ScenarioPath"] ?? "/range/wargame/scenarios"));
builder.Services.AddSingleton(new TopologySource(builder.Configuration["Wargame:TopologyPath"] ?? "/range/topology.yaml"));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"] ?? "/keys"))
    .SetApplicationName("cyber-ducks-rangecontrol");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "rangectl_auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

// Facilitator login: a single shared password (fake, from config). This panel is range control,
// not part of the attack surface (ADR-0008/0011).
app.MapPost("/account/login", async (HttpContext http, IConfiguration config,
    [Microsoft.AspNetCore.Mvc.FromForm] string password, [Microsoft.AspNetCore.Mvc.FromForm] string? returnUrl) =>
{
    if (password != config["Facilitator:Password"])
    {
        return Results.Redirect("/login?error=1");
    }
    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "facilitator")], CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
}).DisableAntiforgery().AllowAnonymous();

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapStaticAssets();
// Pages require auth via AuthorizeRouteView ([Authorize] on Home); the framework/static endpoints
// (blazor.web.js, assets) stay anonymous so the interactive circuit can boot.
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

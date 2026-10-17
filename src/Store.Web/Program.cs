using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Store.Web.Components;
using Store.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration);

// Static server-side rendering only (decision 2): every page is a plain HTTP request/form post,
// so traffic is easy to inspect and replay with an intercepting proxy.
builder.Services.AddRazorComponents();

// Persist Data Protection keys so login cookies survive restarts (keys on a Docker volume).
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"] ?? "/keys"))
    .SetApplicationName("cyber-ducks-web");

// The JWT from store-api is kept inside the (encrypted) auth cookie's token store; the browser
// never sees it (decision 1).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Cookie.Name = "duck_auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Typed access to store-api (flow F4), with the JWT-forwarding handler attached.
builder.Services.AddScoped<CsrfGuard>();
builder.Services.AddScoped<BasketService>();
builder.Services.AddTransient<JwtForwardingHandler>();
builder.Services.AddHttpClient<StoreApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["StoreApi:BaseUrl"]
        ?? throw new InvalidOperationException("StoreApi:BaseUrl is not configured.")))
    .AddHttpMessageHandler<JwtForwardingHandler>();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// No HTTPS redirection or HSTS: the range is plain HTTP on localhost (threat model: accepted risk).

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapStaticAssets();
app.MapAccount();
app.MapRazorComponents<App>();

app.Run();

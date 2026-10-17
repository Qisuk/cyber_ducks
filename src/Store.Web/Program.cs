using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;
using Store.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration);

// Static server-side rendering only (decision 2): every page is a plain HTTP request/form post,
// so traffic is easy to inspect and replay with an intercepting proxy.
builder.Services.AddRazorComponents();

// Typed access to store-api (flow F4). Base address from StoreApi:BaseUrl.
builder.Services.AddHttpClient("store-api", client =>
    client.BaseAddress = new Uri(builder.Configuration["StoreApi:BaseUrl"]
        ?? throw new InvalidOperationException("StoreApi:BaseUrl is not configured.")));

builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// No HTTPS redirection or HSTS: the range is plain HTTP on localhost (threat model: accepted risk).

app.UseAntiforgery();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();

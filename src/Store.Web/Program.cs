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
// never sees it (decision 1). "ExternalCookie" is a short-lived intermediary for the OIDC flow.
var idpPublicHost = builder.Configuration["Oidc:PublicHost"] ?? "localhost:8091";
var idpInternalHost = builder.Configuration["Oidc:InternalHost"] ?? "idp:8080";

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Cookie.Name = "duck_auth";
    })
    .AddCookie("ExternalCookie", options => options.Cookie.Name = "duck_external");

// VULN-OAUTH-01: OIDC state/nonce (CSRF & replay) protection. Read at startup because OIDC options
// are fixed when the handler is built (restart-required toggle).
var oauthStateSecure = await CyberDucks.Shared.Defences.StartupFlags.IsSecureAsync(builder.Configuration, "VULN-OAUTH-01");

// "Sign in with Google / Meta" via the local Keycloak realms (ADR-0003).
foreach (var (scheme, realm, secretKey) in new[]
{
    ("google", "google", "Oidc:GoogleSecret"),
    ("meta", "meta", "Oidc:MetaSecret"),
})
{
    authBuilder.AddOpenIdConnect(scheme, options =>
    {
        options.Authority = $"http://{idpPublicHost}/realms/{realm}";
        options.ClientId = "cyber-ducks-web";
        options.ClientSecret = builder.Configuration[secretKey];
        options.SignInScheme = "ExternalCookie";
        options.ResponseType = "code";
        options.CallbackPath = $"/signin-{scheme}";
        options.SaveTokens = true;
        options.RequireHttpsMetadata = false; // plain HTTP range
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.GetClaimsFromUserInfoEndpoint = true;
        options.TokenValidationParameters.NameClaimType = "name";
        // VULN-OAUTH-01: vulnerable disables state/nonce validation, opening login CSRF and replay.
        if (!oauthStateSecure)
        {
            options.ProtocolValidator.RequireState = false;
            options.ProtocolValidator.RequireStateValidation = false;
            options.ProtocolValidator.RequireNonce = false;
        }
        // Reach Keycloak internally for metadata/token exchange while keeping the public issuer.
        options.BackchannelHttpHandler = new KeycloakBackchannelHandler(idpPublicHost, idpInternalHost)
        {
            InnerHandler = new HttpClientHandler(),
        };
    });
}

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Typed access to store-api (flow F4), with the JWT-forwarding handler attached.
builder.Services.AddScoped<CsrfGuard>();
builder.Services.AddHostedService<KeycloakConfigurator>();
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

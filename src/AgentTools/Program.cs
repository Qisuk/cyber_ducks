using AgentTools;
using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration); // VULN-LLM-02, VULN-LLM-06

// Tools execute inside an HTTP request, so they can read the customer's Authorization header.
builder.Services.AddHttpContextAccessor();

// The only outbound path: store-api. Identity on each call is the VULN-MCP-01 toggle: the customer's
// forwarded token (secure) or a shared service account (vulnerable), chosen in CustomerTokenHandler.
var storeApiBaseUrl = builder.Configuration["StoreApi:BaseUrl"]
    ?? throw new InvalidOperationException("StoreApi:BaseUrl is not configured.");
builder.Services.AddSingleton<ServiceTokenProvider>();
// A plain client (no token handler) used only to log the service account in (VULN-MCP-01 vulnerable).
builder.Services.AddHttpClient("store-login", client => client.BaseAddress = new Uri(storeApiBaseUrl));
builder.Services.AddTransient<CustomerTokenHandler>();
builder.Services.AddHttpClient<StoreApiClient>(client => client.BaseAddress = new Uri(storeApiBaseUrl))
    .AddHttpMessageHandler<CustomerTokenHandler>();

// VULN-EGRESS-01: the fetch_reference tool's outbound client. The guard allows only store hosts when
// secure, and any host when vulnerable (exfil/ingest demo). Defence in depth with the sealed agent net.
builder.Services.AddTransient<EgressGuardHandler>();
builder.Services.AddHttpClient("egress").AddHttpMessageHandler<EgressGuardHandler>();

// The MCP tool server over Streamable HTTP (ADR-0009). Only the allow-listed tool types are exposed.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<OrderTools>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// The agent (ai-agent) is the only client; it reaches this at /mcp (flows F8/F21).
app.MapMcp("/mcp");

app.Run();

// Exposed so the test host can drive the same pipeline.
public partial class Program;

using AgentTools;
using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration); // VULN-LLM-02, VULN-LLM-06

// Tools execute inside an HTTP request, so they can read the customer's Authorization header.
builder.Services.AddHttpContextAccessor();

// The only outbound path: store-api, called as the customer (flow F22, VULN-AGENT-01 toggle later).
builder.Services.AddTransient<CustomerTokenHandler>();
builder.Services.AddHttpClient<StoreApiClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["StoreApi:BaseUrl"]
            ?? throw new InvalidOperationException("StoreApi:BaseUrl is not configured.")))
    .AddHttpMessageHandler<CustomerTokenHandler>();

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

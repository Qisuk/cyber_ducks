using AiAgent;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;
using CyberDucks.Shared.Telemetry;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddRangeTelemetry();
builder.Services.AddDefenceFlags(builder.Configuration); // VULN-LLM-02

// The single IChatClient, backend chosen from config at startup (ADR-0010).
builder.Services.AddSingleton(sp => ChatClientFactory.Create(
    sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddScoped<OrderAgent>();
builder.Services.AddSingleton<ChatRateLimiter>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Log the provider/model in use, so the telemetry and demos show which model answered.
app.Logger.LogInformation("ai-agent using LLM provider {Provider}, model {Model}",
    app.Configuration["Llm:Provider"] ?? "Ollama", app.Configuration["Llm:Model"] ?? "qwen3:4b-instruct");

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// Flow F5: store-web posts the customer's message here, carrying the customer's JWT. The agent
// forwards that token to agent-tools so all data access is scoped to this customer.
app.MapPost("/chat", async Task<IResult> (
    AgentChatRequest request, OrderAgent agent, IDefenceFlags flags, ChatRateLimiter limiter,
    HttpContext http, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { message = "A chat message is required." });
    }
    var token = http.Request.Headers.Authorization.ToString();
    if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        token = token["Bearer ".Length..];
    }
    // VULN-RATELIMIT-01: when secure, cap how fast one caller can hammer the assistant.
    if (flags.IsSecure("VULN-RATELIMIT-01", "ai-agent/chat"))
    {
        var key = string.IsNullOrEmpty(token) ? http.Connection.RemoteIpAddress?.ToString() ?? "anon" : token;
        if (!limiter.Allow(key))
        {
            return Results.Json(new { message = "You're sending messages too quickly — please slow down." },
                statusCode: StatusCodes.Status429TooManyRequests);
        }
    }
    var reply = await agent.ReplyAsync(request, string.IsNullOrEmpty(token) ? null : token, ct);
    return Results.Ok(reply);
});

app.Run();

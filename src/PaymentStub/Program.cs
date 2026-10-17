using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);
builder.AddRangeTelemetry();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

var app = builder.Build();

var webhookUrl = builder.Configuration["StoreApi:WebhookUrl"]
    ?? throw new InvalidOperationException("StoreApi:WebhookUrl is not configured.");
var secret = builder.Configuration["Webhook:Secret"]
    ?? throw new InvalidOperationException("Webhook:Secret is not configured.");

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// A fake payment provider. It always "succeeds" (this is a range), then calls store-api's webhook
// with an HMAC-signed body. store-api decides whether to trust the signature (VULN-WEBHOOK-01).
app.MapPost("/charge", async (ChargeRequest request, IHttpClientFactory clients, ILogger<Program> log, CancellationToken ct) =>
{
    log.LogInformation("Charging {Amount} for invoice {InvoiceId} (ref {Reference})",
        request.Amount, request.InvoiceId, request.Reference);

    var webhook = new PaymentWebhook(request.InvoiceId, request.Reference, "succeeded");
    var body = JsonSerializer.Serialize(webhook);
    var signature = Convert.ToHexStringLower(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    using var message = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
    message.Headers.Add("X-Payment-Signature", signature);

    using var response = await clients.CreateClient().SendAsync(message, ct);
    log.LogInformation("Webhook to store-api for invoice {InvoiceId}: {Status}", request.InvoiceId, (int)response.StatusCode);

    return Results.Ok(new { status = "accepted", request.Reference });
});

app.Run();

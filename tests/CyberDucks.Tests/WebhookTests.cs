using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;

namespace CyberDucks.Tests;

/// <summary>VULN-WEBHOOK-01: whether the payment webhook's signature is checked.</summary>
public sealed class WebhookTests(PostgresFixture db) : IDisposable
{
    private readonly AuthTestFactory _factory = new(db);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    // Places an order as a fresh customer and returns its open invoice id.
    private async Task<int> NewOpenInvoiceAsync()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"buyer-{Guid.NewGuid():N}@example.test", "Buyer", "Correct-Horse-1"), Ct);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var place = await client.PostAsJsonAsync("/api/orders",
            new PlaceOrderRequest("1 Test St", null, [new PlaceOrderItem(1, 1, 4.99m)]), Ct);
        var order = await place.Content.ReadFromJsonAsync<PlaceOrderResult>(Ct);
        var detail = await client.GetFromJsonAsync<OrderDetail>($"/api/orders/{order!.OrderId}", Ct);
        return detail!.Invoice!.Id;
    }

    private async Task<(HttpStatusCode Status, string? InvoiceStatus)> PostWebhookAsync(int invoiceId, bool signed)
    {
        var webhook = new PaymentWebhook(invoiceId, "ref", "succeeded");
        var body = System.Text.Json.JsonSerializer.Serialize(webhook);

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signed)
        {
            var sig = Convert.ToHexStringLower(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(AuthTestFactory.WebhookSecret), Encoding.UTF8.GetBytes(body)));
            message.Headers.Add("X-Payment-Signature", sig);
        }

        var response = await _factory.CreateClient().SendAsync(message, Ct);
        return (response.StatusCode, null);
    }

    [Fact]
    public async Task Vulnerable_accepts_an_unsigned_forged_webhook()
    {
        _factory.SetFlag("VULN-WEBHOOK-01", DefenceMode.Vulnerable);
        var invoiceId = await NewOpenInvoiceAsync();

        var (status, _) = await PostWebhookAsync(invoiceId, signed: false);

        Assert.Equal(HttpStatusCode.OK, status); // invoice marked paid without a real payment
    }

    [Fact]
    public async Task Secure_rejects_an_unsigned_webhook()
    {
        _factory.SetFlag("VULN-WEBHOOK-01", DefenceMode.Secure);
        var invoiceId = await NewOpenInvoiceAsync();

        var (status, _) = await PostWebhookAsync(invoiceId, signed: false);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Secure_accepts_a_correctly_signed_webhook()
    {
        _factory.SetFlag("VULN-WEBHOOK-01", DefenceMode.Secure);
        var invoiceId = await NewOpenInvoiceAsync();

        var (status, _) = await PostWebhookAsync(invoiceId, signed: true);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    public void Dispose() => _factory.Dispose();
}

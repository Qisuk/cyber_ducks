using System.Security.Claims;
using CyberDucks.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Store.Api.Data;
using Store.Api.Orders;

namespace Store.Api.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPayments(this IEndpointRouteBuilder app)
    {
        // Start paying an invoice: hands off to payment-stub, which calls the webhook back.
        app.MapPost("/api/invoices/{id:int}/pay", async Task<IResult> (
            int id, ClaimsPrincipal principal, StoreDbContext db, IOrderAccess access,
            IHttpClientFactory clients, CancellationToken ct) =>
        {
            var userId = OrderEndpoints.CurrentUserId(principal);
            // Ownership via VULN-IDOR-01: find the order that owns this invoice.
            var order = await db.Orders.Include(o => o.Invoice)
                .SingleOrDefaultAsync(o => o.Invoice!.Id == id, ct);
            if (order?.Invoice is null || (await access.FindAsync(order.Id, userId, ct)) is null)
            {
                return Results.NotFound();
            }
            if (order.Invoice.Status != "open")
            {
                return Results.Conflict(new { message = $"Invoice {id} is already {order.Invoice.Status}." });
            }

            var reference = $"pay_{Guid.NewGuid():N}";
            db.Payments.Add(new Payment
            {
                InvoiceId = order.Invoice.Id,
                Amount = order.Invoice.AmountDue,
                ProviderRef = reference,
                Status = "pending",
            });
            await db.SaveChangesAsync(ct);

            var charge = new ChargeRequest(order.Invoice.Id, order.Invoice.AmountDue, reference);
            using var response = await clients.CreateClient("payment-stub").PostAsJsonAsync("charge", charge, ct);
            response.EnsureSuccessStatusCode();
            return Results.Accepted(value: new { reference });
        }).RequireAuthorization();

        // Inbound webhook from payment-stub. Anonymous by design (it's a server-to-server callback);
        // VULN-WEBHOOK-01 decides whether the signature is checked.
        app.MapPost("/api/payments/webhook", async Task<IResult> (
            HttpContext http, StoreDbContext db, IWebhookVerifier verifier, CyberDucks.Shared.Ctf.CtfFlagReader flags,
            ILogger<Program> log, CancellationToken ct) =>
        {
            http.Request.EnableBuffering();
            using var reader = new StreamReader(http.Request.Body, leaveOpen: true);
            var payload = await reader.ReadToEndAsync(ct);

            if (!verifier.Verify(payload, http.Request.Headers["X-Payment-Signature"].FirstOrDefault()))
            {
                log.LogWarning("Rejected payment webhook with an invalid signature");
                return Results.Unauthorized();
            }

            var webhook = System.Text.Json.JsonSerializer.Deserialize<PaymentWebhook>(payload);
            if (webhook is null || webhook.Status != "succeeded")
            {
                return Results.BadRequest();
            }

            var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.Id == webhook.InvoiceId, ct);
            if (invoice is null)
            {
                return Results.NotFound();
            }
            if (invoice.Status == "paid")
            {
                return Results.Ok(); // idempotent
            }

            invoice.Status = "paid";
            invoice.PaidAt = DateTimeOffset.UtcNow;
            var order = await db.Orders.SingleAsync(o => o.Id == invoice.OrderId, ct);
            if (order.Status == "placed")
            {
                order.Status = "paid";
                order.UpdatedAt = DateTimeOffset.UtcNow;
            }
            var payment = await db.Payments
                .Where(p => p.ProviderRef == webhook.Reference)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(ct);
            if (payment is not null)
            {
                payment.Status = "succeeded";
            }
            await db.SaveChangesAsync(ct);
            log.LogInformation("Invoice {InvoiceId} marked paid via webhook", invoice.Id);
            // CTF flag `webhook-forge`: the callback confirmation. In vulnerable mode the signature
            // isn't checked, so a forged "succeeded" callback reaches here and the forger sees this.
            return Results.Ok(new { status = "ok", invoice = invoice.Id, token = await flags.GetAsync("webhook-forge", ct) });
        });

        return app;
    }
}

namespace CyberDucks.Shared.Contracts;

// Payment contracts shared by store-api and payment-stub.

/// <summary>store-api asks payment-stub to charge for an invoice.</summary>
public sealed record ChargeRequest(int InvoiceId, decimal Amount, string Reference);

/// <summary>
/// The webhook payment-stub sends back to store-api. The body is HMAC-signed; whether store-api
/// checks the signature is VULN-WEBHOOK-01.
/// </summary>
public sealed record PaymentWebhook(int InvoiceId, string Reference, string Status);

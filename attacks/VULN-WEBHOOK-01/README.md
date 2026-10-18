# VULN-WEBHOOK-01 — Unverified payment webhook (fake "paid")

| | |
|---|---|
| **Flag** | `VULN-WEBHOOK-01` (code, no restart) |
| **Where** | store-api `POST /api/payments/webhook` → `IWebhookVerifier` ([WebhookVerifier.cs](../../src/Store.Api/Payments/WebhookVerifier.cs)) |
| **Class** | OWASP A08:2021 Software & data integrity failures |
| **Threats** | CT40; asset A14 (invoices / revenue) |

## The weakness

The webhook is anonymous by design (a server-to-server callback from the payment provider), and in
vulnerable mode it **accepts any body, signed or not**. Anyone who can reach the endpoint can POST a
`"succeeded"` event for an invoice id and have it marked **paid** — no money changes hands.

Secure mode requires a valid HMAC-SHA256 signature (`X-Payment-Signature`) over the body, compared
in constant time.

## Business impact

Goods shipped against invoices that were never paid — direct fraud. Integrity of the payment state
depends entirely on this check.

## Confirm it

```bash
bash attacks/VULN-WEBHOOK-01/check.sh
```

The probe places an order (open invoice), POSTs a **forged, unsigned** `succeeded` webhook for that
invoice, and reads the invoice status back. (Creates a test order — `docker compose down -v` resets.)

- **Vulnerable:** the invoice flips to `paid` from the forged, unsigned call.
- **Secure:** the forged call is rejected; the invoice stays `open`.

## Observe in telemetry

store-api logs `Rejected payment webhook with an invalid signature` in secure mode. The webhook span
tags `defence.VULN-WEBHOOK-01`. Signature: a `/api/payments/webhook` request **without** a valid
`X-Payment-Signature`, or from an unexpected source, that still marks an invoice paid. See
[detections/VULN-WEBHOOK-01.md](../../detections/VULN-WEBHOOK-01.md).

## Fix

Flip `VULN-WEBHOOK-01` to **secure**: verify the HMAC signature on every webhook and reject anything
unsigned or mis-signed. Keep the shared secret out of the repo (it lives in `.env`).

# VULN-INFO-01 — Verbose error responses leak internals

| | |
|---|---|
| **Flag** | `VULN-INFO-01` (code, no restart) |
| **Where** | store-api error middleware ([ErrorHandling.cs](../../src/Store.Api/ErrorHandling.cs)) |
| **Class** | OWASP A05:2021 Security misconfiguration |
| **Threats** | TH5; asset A9 (internal details) |

## The weakness

On an unhandled exception the vulnerable path returns the **exception message, type and full stack
trace** in the HTTP body. That hands an attacker the database engine, library versions, SQL
fragments and internal paths — ideal for developing a deeper exploit (it makes SQL-injection payload
development, VULN-SQLI-01, much easier). Secure mode returns a generic message and logs the detail
server-side only.

## Business impact

Information disclosure that accelerates other attacks; stack traces can themselves contain sensitive
data.

## Confirm it

```bash
bash attacks/VULN-INFO-01/check.sh
```

The probe forces a 500 through the product-search path (a broken term). **This only errors when
`VULN-SQLI-01` is vulnerable** — the concatenated SQL has to break — so if SQLI is secure the probe
says so and can't demonstrate here.

- **Vulnerable:** the 500 body contains `stackTrace` / `Npgsql…` / exception `type`.
- **Secure:** the 500 body is `{"message":"An unexpected error occurred."}`.

## Observe in telemetry

The error is logged server-side either way (`Unhandled exception for {Path}`); the span tags
`defence.VULN-INFO-01`. The signature is a `500` **response body** carrying a stack trace — hunt the
response, not just the status. See [detections/VULN-INFO-01.md](../../detections/VULN-INFO-01.md).

## Fix

Flip `VULN-INFO-01` to **secure**: clients get a generic message, details stay in the server logs.
Turning this on also removes the feedback loop an injection attacker relies on.

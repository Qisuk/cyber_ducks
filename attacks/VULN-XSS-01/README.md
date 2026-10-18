# VULN-XSS-01 — Stored XSS in product reviews

| | |
|---|---|
| **Flag** | `VULN-XSS-01` (code, no restart) |
| **Where** | store-web review rendering ([DuckDetail.razor](../../src/Store.Web/Components/Pages/DuckDetail.razor)); storage is raw in store-api |
| **Class** | OWASP A03:2021 — Cross-site scripting (stored) |
| **Threats** | TH8 (also the indirect prompt-injection carrier for the AI agent); assets A1, A17 |

## The weakness

A review body is stored verbatim and, in vulnerable mode, **rendered as raw HTML** on the product
page (`@((MarkupString)review.Body)`). Any customer can post a review containing markup — including
`<script>` — that then runs in **every** visitor's browser (stored/persistent XSS). Secure mode
renders the body as text (Blazor HTML-encodes it).

The same free-text field is the carrier for **indirect prompt injection** against the AI agent
(VULN-LLM-02): text other people wrote flows into the model's context.

## Business impact

Session theft, actions performed as the victim, defacement — triggered just by viewing a product.
Staff viewing reviews are targets too.

## Confirm it

```bash
bash attacks/VULN-XSS-01/check.sh
```

The probe posts a review containing a **benign, inert** marker element (`<i>…</i>`, no script) and
checks how store-web renders it. (Adds a test review — `docker compose down -v` resets.)

- **Vulnerable:** the `<i>` element is live in the page HTML (markup injected → a `<script>` would
  execute).
- **Secure:** the tag is HTML-encoded and shown as text.

## Observe in telemetry

Server-side this looks like a normal review POST, so the signature is in the **content**: review
bodies containing `<`, `>` or `<script`. A content check at ingest (or a WAF in Phase 8) is the
practical detection. See [detections/VULN-XSS-01.md](../../detections/VULN-XSS-01.md).

## Fix

Flip `VULN-XSS-01` to **secure**: render user content as text (encode on output). Add a Content
Security Policy in Phase 8 as defence in depth. Never render stored user input as raw HTML.

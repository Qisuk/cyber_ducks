# VULN-CSRF-01 — No anti-forgery check on state-changing forms

| | |
|---|---|
| **Flag** | `VULN-CSRF-01` (code, no restart) |
| **Where** | store-web `CsrfGuard` on form posts ([CsrfGuard.cs](../../src/Store.Web/Services/CsrfGuard.cs)) |
| **Class** | OWASP A01:2021 — CSRF |
| **Threats** | CT5; assets A1, A6 |

## The weakness

The forms include an `<AntiforgeryToken />`, but in vulnerable mode `CsrfGuard` does **not** validate
it. A state-changing POST is accepted on the auth cookie alone — so a page on another site can
auto-submit a form that acts as the logged-in user (change their profile, place or cancel orders).
The cookie is `SameSite=Lax`, which blunts but does not fully close this. Secure mode validates the
token and rejects a request without a valid one.

## Business impact

Actions performed as the victim without their intent: profile/role changes, unwanted orders,
cancellations.

## Confirm it

```bash
bash attacks/VULN-CSRF-01/check.sh
```

The probe logs in (cookie), then POSTs the profile-change form **without** an anti-forgery token —
what a forged cross-site form would send. (It changes the display name in vulnerable mode; reset
with `docker compose down -v`, or it is cosmetic.)

- **Vulnerable:** `302` — the change is accepted with no token.
- **Secure:** `400` — a valid anti-forgery token is required.

## Observe in telemetry

The form-post span tags `defence.VULN-CSRF-01`. In secure mode, rejected posts show as `400`s on
state-changing routes. The attack signature is a state-changing POST whose `Origin`/`Referer` is a
different site. See [detections/VULN-CSRF-01.md](../../detections/VULN-CSRF-01.md).

## Fix

Flip `VULN-CSRF-01` to **secure**: validate the anti-forgery token on every state-changing form.
Keep cookies `SameSite=Lax`/`Strict` as defence in depth.

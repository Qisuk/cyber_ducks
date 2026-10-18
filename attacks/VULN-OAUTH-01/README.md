# VULN-OAUTH-01 — OIDC state/nonce validation disabled (login CSRF & replay)

| | |
|---|---|
| **Flag** | `VULN-OAUTH-01` (code, **restart required**) |
| **Where** | store-web OIDC options ([Program.cs](../../src/Store.Web/Program.cs), `ProtocolValidator`) |
| **Class** | OWASP A07:2021 — Auth failures |
| **Threats** | CT-OAuth (login CSRF, authorization-code replay); asset A12 |

## The weakness

In vulnerable mode store-web turns off the OIDC `state` and `nonce` checks
(`RequireState`/`RequireStateValidation`/`RequireNonce = false`). `state` is the CSRF defence for the
login flow; `nonce` binds the ID token to this login and stops replay. Without them, an attacker can
mount **login CSRF** (force a victim's browser to complete a login the attacker started, so the
victim acts in the attacker's session) and **replay** an intercepted authorization response. Secure
mode enforces both. This is a **restart** toggle (OIDC options are built at startup).

## Business impact

Session fixation / login CSRF leads to the victim unknowingly using the attacker's account (or vice
versa), and replay weakens the whole federated-login guarantee.

## Confirm it

This lives in the browser's OIDC redirect dance, so confirm it by inspection rather than a script:

1. Set `VULN-OAUTH-01` and **restart store-web** (`docker compose restart store-web`).
2. Start "Sign in with Google" and watch the redirect to the callback (`/signin-google`) with an
   intercepting proxy or the browser devtools Network tab.
3. **Vulnerable:** the callback is accepted even if the `state` parameter is altered or missing.
   **Secure:** a missing/mismatched `state` is rejected (correlation failure).

## Observe in telemetry

store-web logs an OIDC correlation/auth failure in secure mode when `state` doesn't match. Signature:
callbacks accepted with absent/mismatched `state` (vulnerable) vs correlation-failure logs (secure).
See [detections/VULN-OAUTH-01.md](../../detections/VULN-OAUTH-01.md).

## Fix

Flip `VULN-OAUTH-01` to **secure** and restart store-web: enforce `state` and `nonce` on every OIDC
login. These checks are not optional.

# VULN-OAUTH-02 — Loose OAuth redirect-URI (authorization-code capture)

| | |
|---|---|
| **Flag** | `VULN-OAUTH-02` (infra, **restart required**) |
| **Where** | Keycloak client `cyber-ducks-web` redirect URIs, set at startup by [KeycloakConfigurator.cs](../../src/Store.Web/Services/KeycloakConfigurator.cs) |
| **Class** | OWASP A07:2021 — Auth failures; threat CT18 |
| **Threats** | authorization-code capture; asset A12 |

## The weakness

In vulnerable mode the IdP client registers a **wildcard** redirect URI
(`http://localhost:8080/*`), so Keycloak will send the authorization code to **any** path on the
host. If an attacker can get the victim to start a login with a crafted `redirect_uri` that still
matches the wildcard, the code (and thus the session) can be delivered to an attacker-controlled
path. Secure mode registers only the exact callback (`/signin-google`, `/signin-meta`).

Redirect-URI validation lives in the IdP, so the toggle reconfigures Keycloak via its admin API at
startup — a **restart** toggle.

## Business impact

Authorization-code interception → account takeover of the federated login.

## Confirm it

Inspect the IdP client's registered redirect URIs:

1. Set `VULN-OAUTH-02` and **restart store-web** (it reconfigures Keycloak on startup).
2. Check the client config via Keycloak's admin API (admin creds in `.env`), realm `google` or
   `meta`, client `cyber-ducks-web`:
   - **Vulnerable:** `redirectUris` = `["http://localhost:8080/*"]` (wildcard).
   - **Secure:** `redirectUris` = `["http://localhost:8080/signin-google"]` (exact).

In the login flow, a vulnerable client accepts a `redirect_uri` pointing at an unexpected path on the
host; a secure client rejects anything but the exact callback.

## Observe in telemetry

Keycloak logs the authorization request and its `redirect_uri`. Signature: a successful authorize
whose `redirect_uri` is not the exact callback path. See
[detections/VULN-OAUTH-02.md](../../detections/VULN-OAUTH-02.md).

## Fix

Flip `VULN-OAUTH-02` to **secure** and restart store-web: register only exact redirect URIs at the
IdP. Never use wildcard or path-prefix redirect URIs.

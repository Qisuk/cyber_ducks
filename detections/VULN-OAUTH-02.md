# Detect VULN-OAUTH-02 — loose redirect URI

**Signature:** the IdP client registers a wildcard redirect URI, and/or an authorize request
succeeds with a `redirect_uri` that is not the exact callback path.

## Client config (Keycloak admin API)

The direct check is the registered redirect URIs for `cyber-ducks-web` (realms `google`, `meta`):

- wildcard `http://localhost:8080/*` → vulnerable
- exact `http://localhost:8080/signin-google` → secure

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-OAUTH-02 = "vulnerable" }   # read at startup
```

## Authorize requests (Keycloak logs)

Keycloak logs each authorization request with its `redirect_uri`. A `redirect_uri` that is not the
exact callback — an unexpected path on the host — is the red flag.

## Phase 8 / hardening

Register only exact redirect URIs (secure, restart). Alert on authorize requests whose `redirect_uri`
differs from the known callback, and treat any wildcard in client config as a finding.

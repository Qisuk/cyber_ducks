# Detect VULN-OAUTH-01 — missing OIDC state/nonce

**Signature:** an OIDC callback (`/signin-google`, `/signin-meta`) accepted with an absent or
mismatched `state`. In secure mode these fail with a correlation error.

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-OAUTH-01 = "vulnerable" }   # read at startup
```

## Correlation failures (Loki / LogQL, secure mode)

```
{service_name="store-web"} |~ "(?i)correlation failed|oauth state|nonce|OpenIdConnect.*fail"
```

A burst of these means someone is sending callbacks with bad/replayed `state` — login-CSRF or replay
attempts being rejected. In **vulnerable** mode these are absent because nothing is checked, which is
the problem.

## The callback (Tempo)

```
{ span.http.route =~ "/signin-.*" }
```

Inspect for callbacks lacking a `state` parameter, or repeated use of the same authorization code
(replay).

## Phase 8 / hardening

Enforce `state`/`nonce` (secure, restart). Alert on OIDC correlation failures and on repeated
authorization codes.

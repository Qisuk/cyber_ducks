# VULN-SESSION-01 — JWT signature not verified (token forgery)

| | |
|---|---|
| **Flag** | `VULN-SESSION-01` (code, no restart) |
| **Where** | store-api JWT validation ([TogglingSignatureValidator.cs](../../src/Store.Api/Auth/TogglingSignatureValidator.cs)) |
| **Class** | OWASP A07:2021 / A02 — Identification & crypto failures |
| **Threats** | CT1; asset A12 (sessions), A17 (staff privileges) |

## The weakness

In vulnerable mode store-api **does not verify the JWT signature**. An attacker can mint a token
with any claims they like — `sub` of another user, `role: admin` — signed with their **own** key (or
an `alg:none` token with no signature), and store-api accepts it. Full authentication bypass and
impersonation. Secure mode verifies the HS256 signature against the server key in constant time.

## Business impact

Complete account takeover and privilege escalation from an unauthenticated position — the worst
case. Everything that trusts the token (orders, staff endpoints, the agent acting as the customer)
is compromised.

## Confirm it

Forging a token is exactly what the test suite does, so confirm it there (deterministic, no bespoke
forging tool shipped in this repo):

```bash
dotnet test tests/CyberDucks.Tests --filter FullyQualifiedName~SessionForgeryTests
```

[SessionForgeryTests.cs](../../tests/CyberDucks.Tests/SessionForgeryTests.cs) builds a token with
admin claims signed with an **attacker** key and asserts:

- **Vulnerable:** store-api accepts the forged token (the `/api/auth/me` / staff call succeeds).
- **Secure:** the forged token is rejected with `401`.

By hand you would present such a token as `Authorization: Bearer <forged>` to store-api on
`http://localhost:8081`; in vulnerable mode the call succeeds.

## Observe in telemetry

A forged token that is *accepted* leaves little trace — that is the danger. In **secure** mode,
rejects surface as `401`s with `SecurityTokenInvalidSignatureException` in the store-api logs:

```
{service_name="store-api"} |~ "(?i)signature validation failed|invalid.?signature|alg.?none"
```

See [detections/VULN-SESSION-01.md](../../detections/VULN-SESSION-01.md).

## Fix

Flip `VULN-SESSION-01` to **secure**: verify the signature on every token, reject `alg:none`, and
keep the signing key strong and secret (ties in VULN-PWD-01 / key management). Short token lifetimes
limit the blast radius.

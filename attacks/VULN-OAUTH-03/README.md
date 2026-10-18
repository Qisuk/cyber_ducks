# VULN-OAUTH-03 — Social login linked on an unverified email (account takeover)

| | |
|---|---|
| **Flag** | `VULN-OAUTH-03` (code, no restart) |
| **Where** | store-api social linking ([SocialLinking.cs](../../src/Store.Api/Auth/SocialLinking.cs)); `POST /api/auth/social` |
| **Class** | OWASP A07:2021 — Auth failures (account takeover) |
| **Threats** | account takeover; assets A1, A12 |

## The weakness

When a social login (Google/Meta) arrives, the store links it to a local account **by email**. In
vulnerable mode it links even when the provider says the email is **not verified**. So an attacker
can register at a mock IdP with a victim's email address, mark nothing, "Sign in with Meta", and be
logged straight into the victim's store account. Secure mode links only when `email_verified` is
true.

The range seeds exactly this: the Meta realm has **mallory** with email `alice@duckstore.test` and
`emailVerified = false` — the takeover attacker against alice.

## Business impact

Full account takeover with no password needed — just control of (or a claim to) an email at a
federated provider.

## Confirm it

The social callback is internal (store-web ↔ Keycloak ↔ store-api), so confirm it with the
deterministic test:

```bash
dotnet test tests/CyberDucks.Tests --filter FullyQualifiedName~SocialLoginTests
```

[SocialLoginTests.cs](../../tests/CyberDucks.Tests/SocialLoginTests.cs) drives
`POST /api/auth/social` with a verified vs unverified email:

- **Vulnerable:** an unverified-email social identity links to the existing account (takeover).
- **Secure:** an unverified email is refused linking.

End to end in the browser: sign in with Meta as **mallory** (realm `meta`), whose email is alice's;
in vulnerable mode you land in alice's account.

## Observe in telemetry

The `/api/auth/social` span tags `defence.VULN-OAUTH-03`. Signature: a social link where the
provider's `email_verified` is false, or a social identity whose email matches an existing
password account it never owned. See [detections/VULN-OAUTH-03.md](../../detections/VULN-OAUTH-03.md).

## Fix

Flip `VULN-OAUTH-03` to **secure**: link only on a verified email, and prefer linking by the
provider's stable subject id with an explicit, authenticated "connect account" step.

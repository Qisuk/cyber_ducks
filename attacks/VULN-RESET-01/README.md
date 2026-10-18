# VULN-RESET-01 — weak password-reset (guessable token / enumeration)

| | |
|---|---|
| **Flag** | `VULN-RESET-01` (code) |
| **Where** | store-api reset flow ([AuthEndpoints.cs](../../src/Store.Api/Auth/AuthEndpoints.cs), [PasswordReset.cs](../../src/Store.Api/Auth/PasswordReset.cs)); mail lands in Mailpit |
| **Class** | OWASP A07 (identification & auth failures) |
| **Threats** | TH3; asset A1 |

## The weakness

`POST /api/auth/forgot-password` emails a reset link to **Mailpit** (the range's mail catcher,
`http://localhost:8025`). In **vulnerable** mode:

- the reset token is a **guessable 6-digit number** (brute-forceable, and trivial to use if the mail
  is intercepted), valid for an hour; and
- the response **reveals whether the account exists** (`404` vs `200`), enabling account enumeration.

In **secure** mode the token is a 256-bit single-use value with a 15-minute expiry, and the response
is always generic.

## How it is confirmed

1. Request a reset for a seeded account: `POST /api/auth/forgot-password {"email":"alice@duckstore.test"}`.
2. Open Mailpit (`http://localhost:8025`) and read the reset link — the **interception** scenario:
   anyone who can see the mail owns the reset.
3. Use the token: `POST /api/auth/reset-password {"token":"…","newPassword":"…"}` → account taken over.
4. Vulnerable: the 6-digit token is also brute-forceable without the mail; the enumeration response
   tells an attacker which emails are worth attacking.

## Observe in telemetry

store-api logs the reset request and completion (with the user id). A burst of `forgot-password`
calls, or many `reset-password` attempts against one account (token brute force), is the signature.

## Scenarios this enables

- **Password-reset interception → account takeover** (the headline wargame use of Mailpit).
- **Account enumeration** feeding [VULN-AUTH-01](../VULN-AUTH-01/) credential stuffing.
- **Sensitive data in email** / phishing lures (receipts, verify-email) — Mailpit is the tap.

## Fix / design

Strong, single-use, short-lived reset tokens; generic responses that never reveal account existence;
rate-limit reset requests. Never rely on the mail channel being private.

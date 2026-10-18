# VULN-AUTH-01 — No login hardening (enumeration, brute force)

| | |
|---|---|
| **Flag** | `VULN-AUTH-01` (code, no restart) |
| **Where** | store-api `POST /api/auth/login` → `ILoginGuard` ([LoginGuard.cs](../../src/Store.Api/Auth/LoginGuard.cs)) |
| **Class** | OWASP A07:2021 Identification & authentication failures |
| **Threats** | credential stuffing / brute force; asset A2 (credentials) |

## The weakness

Two problems in vulnerable mode:

1. **User enumeration** — a wrong password and an unknown account return **different** messages
   (`"Incorrect password."` vs `"No account with that email."`), so an attacker can confirm which
   emails are registered.
2. **No throttling or lockout** — unlimited attempts per account and per IP, so those confirmed
   accounts can then be brute-forced (helped by the weak seeded passwords `password1`, `letmein`).

Secure mode returns one generic message for both, locks an account for 15 minutes after 5 failures,
and rate-limits each client IP.

## Business impact

Account takeover of customers and staff; the enumeration step makes targeted phishing and stuffing
much easier.

## Confirm it

```bash
bash attacks/VULN-AUTH-01/check.sh
```

The probe compares the response for a known account vs an unknown one — a **non-destructive** signal
that does not lock any account.

- **Vulnerable:** the two messages differ (enumeration).
- **Secure:** identical messages.

The lockout half is **not** exercised by the probe (it would lock a seeded account). To see it, in
vulnerable vs secure mode send ≥5 wrong passwords for one account, then try the correct one: secure
mode refuses it (locked) while vulnerable mode logs you straight in.

## Observe in telemetry

The login span tags `defence.VULN-AUTH-01`. Signatures: many `POST /api/auth/login` from one IP in a
short window (brute force); a run of `401`s across **many distinct emails** (enumeration /
stuffing). See [detections/VULN-AUTH-01.md](../../detections/VULN-AUTH-01.md).

## Fix

Flip `VULN-AUTH-01` to **secure**: generic failure message, per-account lockout, per-IP rate limit.
Pair with strong password policy (VULN-PWD-01) and MFA in a real system.

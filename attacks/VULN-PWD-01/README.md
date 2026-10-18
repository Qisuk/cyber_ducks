# VULN-PWD-01 — Weak password storage (unsalted SHA-256)

| | |
|---|---|
| **Flag** | `VULN-PWD-01` (data, no restart) |
| **Where** | store-api password hashing ([PasswordHashing.cs](../../src/Store.Api/Auth/PasswordHashing.cs)); dual columns `users.password_hash_legacy` / `password_hash` |
| **Class** | OWASP A02:2021 — Cryptographic failures |
| **Threats** | asset A4 (password hashes) |

## The weakness

Vulnerable mode stores and verifies passwords as a **fast, unsalted SHA-256** hash
(`password_hash_legacy`). If that column leaks, the passwords fall quickly: identical passwords share
a hash, rainbow tables and GPU wordlists apply, and the seeded `alice=password1`, `bob=letmein` are
in any wordlist. Secure mode uses PBKDF2 (per-password salt, many iterations) in `password_hash`.

## Business impact

A database leak becomes an immediate mass-account-takeover (and credential-stuffing of other sites,
since people reuse passwords).

## Confirm it

```bash
bash attacks/VULN-PWD-01/check.sh
```

The probe reads the stored legacy hash for alice and shows it equals `sha256("password1")` exactly —
proving the stored value is directly crackable. (Reads the range's postgres via `docker compose
exec`; it only reads.)

- **Observation:** the legacy column is a bare SHA-256; the secure column is PBKDF2. Which one is
  *used* is the VULN-PWD-01 toggle; the weak column exists until the range-control **purge** action
  clears it.

## Observe in telemetry

Storage weakness is at rest, not in traffic — there is no request signature. Detect it by inspecting
the schema/data (the probe's query), and in a real system by secrets-scanning and config review.
See [detections/VULN-PWD-01.md](../../detections/VULN-PWD-01.md).

## Fix

Flip `VULN-PWD-01` to **secure** (PBKDF2) and run the range-control **purge** to drop the legacy
column. Use a slow, salted KDF (PBKDF2/bcrypt/argon2); never a bare fast hash. Enforce strong
passwords (ties in VULN-AUTH-01).

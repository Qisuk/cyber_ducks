# Detect VULN-PWD-01 — weak password storage

Storage weakness is **at rest**, so there is no request signature. Detect by inspecting the data and
config, not traffic.

## In the data (SQL)

```sql
-- Legacy (unsalted SHA-256) hashes present and in use?
SELECT count(*) AS legacy_hashes FROM users WHERE password_hash_legacy IS NOT NULL;
-- Identical hashes for identical passwords (the giveaway of an unsalted scheme):
SELECT password_hash_legacy, count(*) FROM users
WHERE password_hash_legacy IS NOT NULL GROUP BY 1 HAVING count(*) > 1;
```

A bare 64-hex-char value equal to `sha256(password)` is directly crackable.

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-PWD-01 = "vulnerable" }
```

## Phase 8 / hardening

This is prevented, not detected in traffic: use a salted KDF (secure VULN-PWD-01) and run the
range-control **purge** to drop the legacy column. A real pipeline would alert if any new row writes
`password_hash_legacy`.

# Detect VULN-DBROLE-01 — over-privileged DB role

**Signature:** store-api connecting to Postgres as `store_owner` (table owner) instead of the
least-privilege `store_app`; and, downstream, a query reaching tables the app should never touch.

## Which role is store-api using? (Tempo / Loki)

The Npgsql connection carries the DB user. On a trace, the connection span's `db.user` (or the
connection string in config) shows `store_owner` vs `store_app`. Also:

```
{ span.defence.VULN-DBROLE-01 = "vulnerable" }   # tagged at startup read
```

## Reach beyond the app's tables (Tempo)

A `products/search` (or other) span whose executed SQL touches `users`, `saved_cards` or
`audit_log` is a cross-table read — only possible with both an injection (VULN-SQLI-01) and enough
privilege:

```
{ span.db.statement =~ ".*(users|saved_cards|audit_log).*" && span.http.route =~ "/api/products/.*" }
```

## In the database (SQL)

```sql
-- What can each role touch? (least privilege review)
SELECT grantee, table_name, privilege_type FROM information_schema.role_table_grants
WHERE grantee IN ('store_app','store_owner') ORDER BY grantee, table_name;
```

## Phase 8 / hardening

Run as `store_app` (secure, restart). Alert on store-api connecting as `store_owner`, and on any
query from the web path touching `users`/`saved_cards`. Least privilege caps the blast radius of
every other flaw.

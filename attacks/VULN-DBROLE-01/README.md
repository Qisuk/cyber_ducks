# VULN-DBROLE-01 — store-api runs as an over-privileged DB role

| | |
|---|---|
| **Flag** | `VULN-DBROLE-01` (code, **restart required**) |
| **Where** | store-api connection string selection ([Program.cs](../../src/Store.Api/Program.cs)); roles in [01-roles.sh](../../infra/postgres/init) |
| **Class** | OWASP A05:2021 — Security misconfiguration (least privilege) |
| **Threats** | asset A1/A2; amplifies VULN-SQLI-01 |

## The weakness

Vulnerable mode connects to Postgres as **`store_owner`**, which owns every table — far more than the
app needs. So if anything goes wrong (a SQL injection, a logic bug), the blast radius is the whole
`duckstore` database: read and write every table, including `users`, `saved_cards`, `audit_log`.
Secure mode connects as **`store_app`**, a least-privilege role with only the grants the app needs
(see [05-seed grants](../../infra/postgres/init)) — no ownership, no access to what it shouldn't
touch.

This is a **restart** toggle: the connection role is fixed when store-api starts.

## Business impact

Least privilege is the control that limits how bad *other* flaws can get. Over-privilege turns a
contained injection into a full-database compromise.

## Confirm it

This is best shown **in combination** with VULN-SQLI-01:

1. Set `VULN-SQLI-01` vulnerable. Run `attacks/VULN-SQLI-01/check.sh` — injection works regardless of
   role.
2. With `VULN-DBROLE-01` **vulnerable** (store_owner), a UNION-based injection can read other tables
   (e.g. `users`, `saved_cards`).
3. Flip `VULN-DBROLE-01` to **secure** and **restart store-api**
   (`docker compose restart store-api`). The same injection now fails or returns nothing for tables
   `store_app` may not read — the damage is bounded.

You can see the active role's reach by what the injected query can select.

## Observe in telemetry

The Npgsql connection/span shows the database user. Signature: store-api connecting as `store_owner`
(over-privileged) vs `store_app`. A successful cross-table read via `products/search` indicates both
SQLI and over-privilege. See [detections/VULN-DBROLE-01.md](../../detections/VULN-DBROLE-01.md).

## Fix

Flip `VULN-DBROLE-01` to **secure** and restart store-api: run as least-privilege `store_app`. Grant
each service only the privileges it needs; never run the app as the table owner or a superuser.

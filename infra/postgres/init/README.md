# Postgres init scripts

Mounted into the container at `/docker-entrypoint-initdb.d`. The official Postgres image runs
every `*.sql` and `*.sh` file here **once, in filename order, and only when the data volume is
empty**: the "seeded on first start" behaviour (ADR-0002). Design: [docs/data-model.md](../../../docs/data-model.md).

| File | Purpose |
|------|---------|
| `01-roles.sh` | Service roles (passwords from `.env`), database access, the `range_control` database |
| `02-schema.sql` | `duckstore` tables, owned by `store_owner`; least-privilege grants for `store_app` |
| `03-guards.sql` | Toggleable database guards and `set_guard()` (all off by default) |
| `04-range-control.sql` | `range_control` tables: defence flags, presets, change history + notify trigger |
| `05-seed.sql` | Deterministic personas, ducks, orders in every status, invoices, payments, test cards, defence flags and presets |

To re-run them after a change, wipe the volume: `docker compose down -v && docker compose up -d`.
Scripts must use LF line endings (enforced by `.gitattributes`), or they fail inside the container.

# Postgres init scripts

Mounted into the container at `/docker-entrypoint-initdb.d`. The official Postgres image runs
every `*.sql` and `*.sh` file here **once, in filename order, and only when the data volume is
empty**: the "seeded on first start" behaviour (ADR-0002).

Planned files:

| File | Purpose |
|------|---------|
| `01-schema.sql` | Tables, types, constraints |
| `02-roles.sql` | Database roles used by each service |
| `03-seed.sql` | Deterministic sample data (personas, ducks, orders) |

To re-run them after a change, wipe the volume: `docker compose down -v && docker compose up -d`.
Scripts must use LF line endings (enforced by `.gitattributes`), or they fail inside the container.

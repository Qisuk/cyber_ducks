# ADR-0002: Postgres seeding via init scripts; EF Core + deliberate raw SQL

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
The database must be seeded with sample customers/orders the first time it starts, and the
app needs both "normal" and deliberately injectable data-access paths.

## Options
- Seeding: (a) `docker-entrypoint-initdb.d` SQL scripts; (b) EF Core migrations + C# seeder.
- Access: (a) EF Core only; (b) Dapper/Npgsql only; (c) EF Core + selective raw SQL.

## Decision
- Seed with SQL scripts in `infra/postgres/init/`. Postgres runs them **only when the data
  volume is empty**, which gives "first start only" behaviour for free. Reset with `docker compose down -v`.
- Seed data is **deterministic** (fixed IDs, named personas such as Alice and Bob) so demos are repeatable.
- EF Core for most access. Hand-written Npgsql/Dapper SQL for the paths that are deliberately injectable.

## Consequences
- Schema is owned by SQL scripts, not EF migrations; EF maps onto the existing schema.
- Schema changes need a volume reset during development (acceptable for a range).

# Cyber Ducks

> [!CAUTION]
> **This project is deliberately vulnerable. It is a training target, not an example to follow.**
>
> - **Never deploy it** to a server, cloud account, or any network-reachable host. Run it only on
>   your own machine. All ports are bound to `127.0.0.1`; do not change that.
> - **Do not copy code from it** into real projects. Much of it is insecure on purpose (see the
>   vulnerability toggles in [ADR-0006](docs/decisions/0006-vulnerability-toggles.md)).
> - **All secrets, credentials, card numbers and personal data in this repo are fake.** Never add
>   real ones.
> - **Only attack systems you own or are explicitly authorised to test.** The techniques here are
>   for use against this local range, not anyone else's systems.
>
> Provided as-is for education, with no warranty ([MIT License](LICENSE)). You are responsible
> for how you use it.

A cyber range: a deliberately vulnerable, fully local web store ("Rubber Duck Emporium") built from Docker
services, used to practise cyber/AI attack and defence, threat modelling, and incident response.

## Layout

| Path          | Contents                                                        |
|---------------|-----------------------------------------------------------------|
| `docs/`       | Architecture, threat model, decision records (`docs/decisions`) |
| `compose.yaml` | The range's Docker services                                    |
| `infra/`      | Postgres init/seed scripts, backup scripts, `topology.yaml`; later telemetry & IdP config |
| `src/`        | .NET 10 solution: Store.Web, Store.Api, Store.Agent, AgentTools, PaymentStub, RangeControl |
| `attacks/`    | One folder per vulnerability: description, reproduction, expected telemetry |
| `detections/` | Alert rules / queries that detect the attacks                   |
| `scripts/`    | Repo tooling: diagram rendering, topology checks (`npm run diagrams`, `npm run topology:check`) |

## Running the range

Needs Docker Desktop. Copy `.env.example` to `.env` first (all values are fake range credentials).

| Task | Command |
|------|---------|
| Start (builds the .NET images on first run) | `docker compose up -d --build` |
| The store | http://127.0.0.1:8080 |
| Run the tests (needs Docker) | `dotnet test --solution CyberDucks.slnx` |
| Stop (keep data) | `docker compose down` |
| Reset everything (re-seed on next start) | `docker compose down -v` |
| Explore the database | `psql -h 127.0.0.1 -U duckadmin -d duckstore` (password in `.env`) |
| Telemetry (Grafana) | http://127.0.0.1:3000 → Dashboards → Cyber Ducks → **Range health** |
| Defence toggle panel | http://127.0.0.1:8090 (facilitator password in `.env`) |
| Reload Grafana dashboards after editing `infra/grafana/dashboards/*.json` | `docker compose restart lgtm` |
| Take a backup now | `docker compose exec backup bash /backup-scripts/backup.sh --once` |
| Restore the latest backup | `docker compose exec postgres bash /backup-scripts/restore.sh` |
| Restore a specific backup | `docker compose exec postgres bash /backup-scripts/restore.sh duckstore-<timestamp>.dump` |

**Git Bash on Windows** rewrites arguments that start with `/` into Windows paths, which breaks the
`/backup-scripts/...` commands. Run them from PowerShell, or prefix them with `MSYS_NO_PATHCONV=1`.

## Status

Phases 1–2 done: Postgres (schema, guards, seed, backups) and the telemetry stack (Grafana "Range health"). See [docs/architecture.md](docs/architecture.md).

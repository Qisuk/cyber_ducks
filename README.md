# Rubber Duck Cyber Range

A deliberately vulnerable, fully local web store ("Rubber Duck Emporium") built from Docker
services, used to practise cyber/AI attack and defence.

> **WARNING:** This system is intentionally insecure. Run it only on your own machine.
> All ports are bound to `127.0.0.1`. Never expose it to a network or the internet, and never
> put real credentials or personal data in it.

## Layout

| Path          | Contents                                                        |
|---------------|-----------------------------------------------------------------|
| `docs/`       | Architecture, threat model, decision records (`docs/decisions`) |
| `infra/`      | Docker Compose, Postgres init/seed scripts, telemetry & IdP config |
| `src/`        | .NET 10 solution: Store.Web, Store.Api, Store.Agent, PaymentStub |
| `attacks/`    | One folder per vulnerability: description, reproduction, expected telemetry |
| `detections/` | Alert rules / queries that detect the attacks                   |

## Status

Phase 0: threat modelling & design. See [docs/architecture.md](docs/architecture.md).

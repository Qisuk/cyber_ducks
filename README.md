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
| `infra/`      | Docker Compose, Postgres init/seed scripts, telemetry & IdP config |
| `src/`        | .NET 10 solution: Store.Web, Store.Api, Store.Agent, AgentTools, PaymentStub, OpsConsole |
| `attacks/`    | One folder per vulnerability: description, reproduction, expected telemetry |
| `detections/` | Alert rules / queries that detect the attacks                   |

## Status

Phase 0: threat modelling & design. See [docs/architecture.md](docs/architecture.md).

# Architecture

## Services

| Service          | Image / tech                         | Role                                                  | Est. RAM |
|------------------|--------------------------------------|-------------------------------------------------------|----------|
| `proxy`          | nginx                                | Single entry point on `127.0.0.1`; future WAF location | 20 MB    |
| `store-web`      | .NET 10 Blazor Web App               | Customer UI, staff UI, chat widget                    | 200 MB   |
| `store-api`      | .NET 10 ASP.NET Core Web API         | Auth, products, orders, invoices, shipping            | 200 MB   |
| `ai-agent`       | .NET 10 + Microsoft.Extensions.AI    | Customer chat agent with order-lookup tools           | 150 MB   |
| `ollama`         | ollama/ollama                        | Local LLM inference (CPU)                             | 3–4 GB   |
| `postgres`       | postgres                             | Store database, seeded on first start                 | 150 MB   |
| `idp`            | Keycloak                             | Local OIDC provider posing as "Google" and "Meta"     | 600 MB   |
| `payment-stub`   | .NET 10 minimal API                  | Fake payment gateway + webhooks                       | 80 MB    |
| `mailpit`        | axllent/mailpit                      | Captures outbound email (password resets etc.)        | 30 MB    |
| `otel-collector` + `lgtm` | grafana/otel-lgtm           | Logs (Loki), traces (Tempo), metrics (Prometheus), Grafana | 1–1.5 GB |
| `cadvisor`       | cAdvisor                             | Per-container health/resource metrics                 | 80 MB    |
| `postgres-exporter` | prometheus-community              | Database health metrics                               | 30 MB    |
| `ops-console`    | .NET 10 Blazor (interactive server)  | Wargame: infra map, injects, decision log, response actions. **Not an attack target** ([ADR-0008](decisions/0008-wargame-and-ops-console.md)) | 200 MB |

**Budget:** ~6.5–8 GB of the 9.7 GB Docker allocation. The LLM is the dominant cost; see
[ADR-0004](decisions/0004-ai-agent-ollama.md).

`infra/topology.yaml` will be the single source of truth for services, networks and trust
boundaries. It drives the Ops Console map and the diagrams below.

## Data flow (draft: refine during threat modelling)

```mermaid
flowchart LR
    U[Customer browser] -->|HTTP| P[proxy]
    S[Staff browser] -->|HTTP| P
    P --> W[store-web]
    W -->|REST + JWT| A[store-api]
    W -->|chat| G[ai-agent]
    G -->|tool calls| A
    G -->|prompts| O[ollama]
    A -->|SQL| D[(postgres)]
    U -.->|OIDC redirect| I[idp / Keycloak]
    A -->|token validation| I
    A -->|charge| PS[payment-stub]
    PS -->|webhook| A
    A -->|SMTP| M[mailpit]
    W & A & G & PS -->|OTLP| T[otel-collector / LGTM]
    F[Facilitator / players] -->|ops network only| C[ops-console]
    C -->|alert state| T
    C -.->|response actions: isolate, restore| D
```

## Phases

| # | Phase | Status |
|---|-------|--------|
| 0 | Threat model & design | **In progress** |
| 1 | Skeleton: compose, Postgres schema + seed | |
| 2 | Telemetry stack + container health | |
| 3 | Store API + UI with local login | |
| 4 | Order lifecycle: cancel, invoices, payment stub, staff shipping | |
| 5 | OAuth via Keycloak ("Google"/"Meta") | |
| 6 | AI agent | |
| 7 | Arm the range: vulnerability catalogue + attack scripts | |
| 8 | Defence: detections, hardening, WAF, segmentation | |
| 9 | Wargame: Ops Console, scenarios, injects, role cards, after-action report | |

Groundwork for Phase 9 is laid earlier: audit events and trace IDs (Phases 2–3), backups and
restore scripts (Phase 1 onward), and `topology.yaml` (Phase 1).

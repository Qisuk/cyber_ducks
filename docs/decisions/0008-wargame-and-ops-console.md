# ADR-0008: Hybrid wargame with a Blazor Ops Console

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
The project ends with a wargame: "the site has been compromised by an unknown adversary;
what do you do?" Players should practise incident response at both the technical and the business
level. It is used solo for now but should support multiple participants later.

## Options
- Style: (a) pure tabletop; (b) live-fire; (c) **hybrid**: scenarios run on the range, and players
  investigate real telemetry but make decisions at tabletop level.
- Map: (A) Grafana Canvas/Node Graph; (B) **custom Blazor Ops Console**; (C) both.

## Decision
Hybrid wargame (c) with a custom Blazor **Ops Console** (B). Grafana remains the tool for
detailed investigation.

### Ops Console
- **Interactive infra map** with two layers:
  - *Detected:* nodes coloured by alert state, pulled from the Grafana alerting API.
  - *Ground truth (facilitator only):* what is actually compromised. Attack scenario scripts
    report state changes to the console, and the facilitator can override manually.
  - The gap between the two layers is a core learning outcome.
- **Facilitator controls:** start a scenario, release timed injects, reveal ground truth during the debrief.
- **Decision log:** timestamped record of every player decision, which feeds the after-action report.
- **Response actions:** buttons that run real containment/recovery scripts (isolate a container,
  restore the DB from backup, rotate a secret), so decisions have consequences on the range.
- **Topology source of truth:** `infra/topology.yaml` (services, networks, trust boundaries)
  drives the map and the architecture/threat-model diagrams.

### Multi-participant readiness
- Build for one player first, but model **sessions, participants and roles** in the data from day one
  (incident commander, tech lead, comms, legal/DPO, exec, facilitator).
- Blazor interactive server rendering (SignalR) gives every participant shared, live state for free.
- Role-based views: each role sees different injects; only the facilitator sees ground truth.

### Not a target
The Ops Console is **range infrastructure, not part of the attack surface**. It runs on its own
`ops` network and port, has its own auth, is reasonably hardened, and is excluded from attack
scope. If players could tamper with the game master's view, the exercise would be worthless.

## Update (2026-10-18): basics built inside range-control
- **Hosted in `range-control`, not a new `ops-console` service.** It already sits alone on the
  isolated `ops` network with facilitator auth and interactive Blazor, so this saves ~200 MB and a
  second login. It can be split out later if participants need their own logins.
- **Data:** tables in the `range_control` database (`06-wargame.sql`, see `docs/data-model.md`);
  `flags_reader` cannot read them. Scenarios are YAML files in `wargame/scenarios`, mounted
  read-only so they can be edited without a rebuild.
- **Map:** drawn as SVG from `infra/topology.yaml`, zones in fixed columns. Fill = detected state,
  thick outline = ground truth (facilitator only). States: normal, suspected, compromised,
  contained, recovered. Clicking a node highlights its flows and opens its state editor.
- **Roles:** an "acting as" selector gives each role's view (only the facilitator sees ground truth,
  facilitator notes and unreleased injects). Open consoles refresh live on every change.
- **After-action report:** detected-vs-truth gap, decisions by NIST phase, one timeline of injects,
  decisions, map changes and defence-flag changes made during the exercise. Times are UTC.
- **Detected layer from alerts (Phase 8):** `GrafanaAlertBridge`, a hosted service in range-control,
  polls Grafana's firing detection alerts (flow F19) and raises the mapped node to `suspected`
  (`compromised` for a critical rule) on the detected layer for every running exercise. The alert →
  node mapping is a `range_node` label on each rule in `range-alerts.yaml`, so it stays next to the
  detection. Raise-only from `normal`: once a human (or an earlier tick) has moved a node, the team
  owns it — the bridge never downgrades or clears it, keeping the facilitator in control of the map.
  If Grafana is unreachable or anonymous access is off with no token, the layer simply stays manual.
- **Deferred:** response
  action buttons (isolating containers needs the Docker socket, which is close to host root —
  needs its own design); per-participant logins; attack scripts reporting ground truth (Phase 7).

## Consequences
- Wargame state lives in the existing `range_control` database (see the update above); no new
  service for now.
- Telemetry must support investigation: end-to-end trace IDs, audit events for security-relevant
  actions, UTC timestamps, and retention that covers a whole exercise.
- Recovery must be real: scheduled `pg_dump` backups and container isolate/rebuild scripts.
- The threat model gains a business context (fictional company profile, regulatory obligations).
- New Phase 9 (wargame) with materials: scenarios, injects, role cards, decision log, after-action
  report template. It follows the NIST SP 800-61 incident-response lifecycle.

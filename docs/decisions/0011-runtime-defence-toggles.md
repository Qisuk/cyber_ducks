# ADR-0011: Runtime defence toggles controlled from a range-control panel

- **Status:** Accepted
- **Date:** 2026-10-17
- **Amends:** ADR-0006 (replaces its environment-variable flag mechanism; vuln IDs and the
  `attacks/` / `detections/` structure stay)

## Context
Demonstrating a fix is most convincing when a defence can be switched on mid-attack and the
effect seen immediately in the app and in telemetry. ADR-0006's environment-variable flags need
a container restart per change and offer no single place to see or change the range's state.

## Decision

### 1. Every defence is a runtime toggle
- Each vulnerability ID has a flag: `vulnerable` or `secure`.
- Code uses one interface per affected behaviour with **two implementations side by side**
  (`Insecure…` / `Secure…`). A resolver picks one per request from the current flag
  (strategy pattern). Comparing the two files shows exactly what the fix is.
- Services cache flags briefly and refresh on change (Postgres `LISTEN/NOTIFY`), so a flip takes
  effect within seconds.
- **Presets** set many flags at once: *all vulnerable*, *all secure*, and per-scenario sets for the wargame.

### 2. The panel lives in range control (Z9), not in the store
- A minimal **`range-control`** service is built for the alpha and grows into the Ops Console
  (ADR-0008) in Phase 9.
- It runs on its own network and port with its own login, and is **excluded from attack scope**.
  If the store hosted the panel, an attacker could simply switch the defences off.

### 3. Flags live in a separate database
- Database `range_control`, owned by role `range_control_owner`. Store roles have **no CONNECT
  privilege** on it, so even a fully compromised store database role can't flip defences.
- Store services read flags through a read-only role that can see only the flags table.
- **No application ever connects as the Postgres superuser** (`POSTGRES_USER`). The "vulnerable"
  store role is over-privileged *within* `duckstore` (owner), but is not a cluster superuser.

### 4. Three kinds of toggle
| Kind | Examples | Behaviour |
|------|----------|-----------|
| Code path | Object-level authorisation, mass assignment, query construction, CSRF, agent tool scoping | Switches within seconds |
| Infrastructure config | Proxy routing, Grafana/Keycloak settings, network segmentation | Panel reconfigures and restarts the container; shown as "restart required" |
| Data at rest | Password storage, stored card data | Schema holds **both** representations and the flag switches which is used. A separate **one-way "purge insecure data"** action removes the insecure copies, reversible only by resetting the range |

The purge action makes a deliberate teaching point: a flag changes *behaviour*, but fixing data
already exposed needs a migration that destroys the insecure copies.

### 5. Observability and testing
- Every flag change and preset is logged as an audit event and pushed to Grafana as an annotation,
  so dashboards show "defence X enabled at 14:02".
- Each `attacks/<VULN-ID>/` check runs in both modes: it must **succeed** when the flag is
  `vulnerable` and **fail** when it is `secure`. This proves both implementations work.

## Consequences
- Roughly double the implementation and test work per vulnerability. Accepted: this is the core
  purpose of the range.
- New service `range-control` (~150 MB) in the alpha; the `ops` network is introduced early.
- Flag combinations can interact. Dependencies between flags are documented per vuln ID.
- In "secure" mode, insecure data remains until purged. The panel shows this clearly.

# VULN-LOG-01 — No audit logging of security events

| | |
|---|---|
| **Flag** | `VULN-LOG-01` (code) — **defined, not yet wired** (planned) |
| **Where** | `audit_log` table ([02-schema.sql](../../infra/postgres/init/02-schema.sql)); emission is planned |
| **Class** | OWASP A09:2021 — Security logging & monitoring failures |
| **Threats** | TH3, TH12; asset A9 |

## Status

The `audit_log` table exists but is **deliberately unused in the alpha**. The flag is on the panel;
structured security-event emission is on the roadmap. Note the range still has rich **telemetry**
(OTel traces/logs/metrics to Grafana) — the detections throughout this catalogue rely on it. LOG-01
is specifically about a durable, queryable **audit trail** of security-relevant actions.

## The intended weakness

Vulnerable mode: security-relevant actions (logins, role changes, cancellations, payments, social
links, admin actions) are **not** recorded as audit events, so after an incident there is no reliable
trail of who did what. This slows detection and makes the wargame's "when did we become aware?"
question hard to answer. Secure mode writes an `audit_log` row (with trace id) for each such action.

## Why it matters here

The wargame scenario *Other people's ducks* hinges on reconstructing the timeline. Without an audit
trail, responders must piece it together from raw telemetry — a deliberate learning point, and the
reason several detections in this catalogue say "if VULN-LOG-01 is on, the audit event is the direct
hit".

## How it will be confirmed

Perform a security-relevant action (e.g. a role change via VULN-MASS-01) and check whether an
`audit_log` row was written:

```sql
SELECT occurred_at, user_id, action, object_type, object_id, trace_id FROM audit_log ORDER BY occurred_at DESC LIMIT 20;
```

Vulnerable: empty. Secure: an event per action.

## Observe in telemetry

Even without the audit table, the actions appear in OTel traces/logs (used by the other detections).
The gap LOG-01 fixes is a **durable, security-specific** record that outlives trace retention.

## Fix / design

Emit a structured audit event (with trace id, UTC, actor, action, object) for every
security-relevant action; retain it for the length of an exercise/investigation. Feed it into the
wargame after-action timeline.

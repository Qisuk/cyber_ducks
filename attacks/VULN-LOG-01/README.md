# VULN-LOG-01 — No audit logging of security events

| | |
|---|---|
| **Flag** | `VULN-LOG-01` (code) — **wired** (Phase 8) |
| **Where** | `audit_log` table ([02-schema.sql](../../infra/postgres/init/02-schema.sql)); emission in [AuditLog.cs](../../src/Store.Api/Audit/AuditLog.cs) |
| **Class** | OWASP A09:2021 — Security logging & monitoring failures |
| **Threats** | TH3, TH12; asset A9 |

## Status

**Wired (Phase 8).** Secure mode writes a structured `audit_log` row (with the current trace id) for
each security-relevant action via [AuditLog.cs](../../src/Store.Api/Audit/AuditLog.cs); vulnerable mode
is a no-op (`NoAuditLog`). Currently emitted: `login.success` / `login.failure` / `login.blocked`,
`role.change` (the VULN-MASS-01 escalation lands here), and `order.cancel`. The range also has rich
**telemetry** (OTel to Grafana) that the catalogue's detections rely on; LOG-01 is specifically the
durable, queryable **audit trail** that outlives trace retention.

## The weakness

Vulnerable mode records **no** audit events, so after an incident there is no reliable trail of who
did what — slowing detection and making the wargame's "when did we become aware?" question hard to
answer. Secure mode writes an `audit_log` row per security-relevant action.

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

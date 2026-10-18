# Detect VULN-LOG-01 — missing audit trail for security events

**Signature:** a security-relevant action happened (login, role change, order cancel) but **no
`audit_log` row** records it. VULN-LOG-01 is itself a detection *capability*: in secure mode the
audit trail is the direct, durable hit for several other detections; in vulnerable mode that trail
is empty and you must reconstruct the timeline from raw telemetry.

## Is the defence off right now? (range_control / audit table)

The audit trail lives in the `audit_log` table (append-only; store_app has INSERT only). Query it
directly — in **secure** mode it has an event per security-relevant action, in **vulnerable** mode
it is empty:

```sql
SELECT occurred_at, user_id, action, object_type, object_id, trace_id
FROM audit_log ORDER BY occurred_at DESC LIMIT 50;
```

Currently emitted: `login.success` / `login.failure` / `login.blocked`, `role.change` (the
VULN-MASS-01 escalation lands here), and `order.cancel`. Each row carries the current **`trace_id`**,
so an audit event joins straight to its distributed trace in Tempo.

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-LOG-01 = "vulnerable" }
```

## Fallback when the trail is empty (Tempo / Loki)

Without the audit table, the same actions still appear in traces and logs — this is how the other
detections work, just without a durable, queryable record:

```
{ span.http.url =~ ".*/api/auth/login.*" }                          # logins (Tempo)
{service_name="store-api"} |= "/api/users/" |~ "(?i)role"           # profile/role changes (Loki)
```

The point of VULN-LOG-01 is that these are transient (bounded by trace/log retention), whereas the
audit trail is meant to outlive an exercise or investigation.

## Turn it into an alert

Alert on **high-value audit events** once secure mode is on — e.g. any `role.change`, or a
`login.success` immediately after a burst of `login.failure` (ties in [VULN-AUTH-01](VULN-AUTH-01.md)
and [VULN-MASS-01](VULN-MASS-01.md)). The real control is emitting the structured event in the first
place (secure); detection then has a reliable record to read instead of piecing it together.

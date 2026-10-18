# Detect VULN-MASS-01 — role field in a profile update

**Signature:** a `PUT /api/auth/profile` whose **request body contains a `role` field** (normal
profile edits never send one), especially followed by that user appearing on `/api/staff/*` routes.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-MASS-01 = "vulnerable" }
```

## Privilege change (Loki / LogQL)

A customer whose role becomes `admin`/`employee` without an administrator action. If audit logging
(VULN-LOG-01) is on, a role-change event from a self-service profile update is the direct hit:

```
{service_name="store-api"} |~ "(?i)role.*(admin|employee)"
```

## Follow-on (Tempo)

The newly-promoted user reaching staff functions:

```
{ span.http.route =~ "/api/staff/.*" && span.http.response.status_code = 200 }
```

Correlate the `user.id` with the recent profile update.

## Phase 8 alert idea

Any role change not originating from an admin endpoint → alert. Emit a structured audit event on
role change (ties in VULN-LOG-01).

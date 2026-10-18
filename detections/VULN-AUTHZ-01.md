# Detect VULN-AUTHZ-01 — customer reaching staff endpoints

**Signature:** a request to a `/api/staff/*` route whose caller holds the `customer` role returns
`200`. A customer should never get a success from a staff function.

## Staff-route calls (Tempo / TraceQL)

```
{ span.http.route =~ "/api/staff/.*" }
```

Inspect the caller: a span with `user.role = "customer"` (or a `sub` that maps to a customer) and
`http.response.status_code = 200` on a staff route is the hit. In secure mode these return `403`.

## Is the defence off right now? (Tempo)

```
{ span.defence.VULN-AUTHZ-01 = "vulnerable" }
```

## Forbidden vs allowed (Prometheus / PromQL)

In secure mode, customer calls to staff routes show up as `403`s. A staff route suddenly returning
`200` to many callers suggests the guard is open:

```
sum by (http_response_status_code) (rate(http_server_request_duration_seconds_count{http_route=~"/api/staff/.*"}[5m]))
```

## Phase 8 alert idea

`200` on `/api/staff/*` where the caller's role is `customer` → alert. The role is on the
authenticated principal; surface it as a span attribute to make this precise.

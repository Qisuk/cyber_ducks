# Detect VULN-AUTH-01 — brute force / enumeration on login

**Signature:** many `POST /api/auth/login` failures from one client IP in a short window (brute
force), or `401`s spread across **many distinct emails** (enumeration / credential stuffing).

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-AUTH-01 = "vulnerable" }
```

## Failure rate per client (Prometheus / PromQL)

```
sum(rate(http_server_request_duration_seconds_count{http_route="/api/auth/login",http_response_status_code="401"}[5m]))
```

A sustained spike of login `401`s is the first signal. In secure mode you would also see `429`s
(rate limited) appear.

## Enumeration (Loki / LogQL)

```
{service_name="store-api"} |= "/api/auth/login" |~ "(?i)no account with that email|incorrect password"
```

Those distinct messages only exist in **vulnerable** mode; seeing them at all means enumeration is
possible. A burst of them across many emails is active enumeration.

## Phase 8 alert idea

Alert on login `401` rate per IP over a threshold, and on `429`s appearing (secure mode throttling
firing). Feed repeat offenders to the WAF/edge in Phase 8.

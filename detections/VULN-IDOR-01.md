# Detect VULN-IDOR-01 — order-id enumeration

**Signature:** one customer (one JWT / client) fetching **many distinct** `/api/orders/{id}` values
in a short window. Normal customers read a handful of their own orders.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-IDOR-01 = "vulnerable" }
```

Any store-api span resolving `IOrderAccess` tags this. Confirms the range is exposed; it is not an
attack by itself.

## Enumeration signature (Tempo)

Spans for the order-by-id route:

```
{ name = "GET /api/orders/{id}" }
```

Then look across a few minutes for the same `user.id` / client hitting a wide spread of ids,
including `404`s (misses) and ids the caller does not own. The mix of many distinct ids + misses
from a single caller is the tell.

## Volume view (Prometheus / PromQL)

A burst of requests to the order-detail route, well above a single customer's normal rate:

```
sum by (http_route) (rate(http_server_request_duration_seconds_count{http_route="/api/orders/{id}"}[5m]))
```

A sustained spike on this route (as in wargame inject I2, "odd traffic on the orders API") is the
thing to alert on.

## Phase 8 alert idea

Rate of distinct `/api/orders/{id}` ids per user over 5 min above a threshold → warning. Needs the
user id on the span/metric (already on the trace).

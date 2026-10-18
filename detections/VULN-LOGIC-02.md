# Detect VULN-LOGIC-02 — cancel after shipping

**Signature:** a successful `POST /api/orders/{id}/cancel` for an order whose status was already
`shipped` or `delivered`.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-LOGIC-02 = "vulnerable" }
```

## The illegal transition (SQL / data)

An order that is `cancelled` yet has a `shipped_at` timestamp is the footprint:

```sql
SELECT id, status, shipped_at FROM orders WHERE status = 'cancelled' AND shipped_at IS NOT NULL;
```

If DB guard `VULN-GUARD-05` is on, this transition is blocked at the database and the cancel fails
with a guard violation in the store-api logs (defence in depth) — look for that instead.

## Cancel spans (Tempo)

```
{ name = "POST /api/orders/{id}/cancel" && span.http.response.status_code = 200 }
```

Correlate with the order's prior state.

## Phase 8 alert idea

Alert on any `cancelled` order with a non-null `shipped_at`. Enforce the state machine in code
(secure VULN-LOGIC-02) and keep the DB guard on.

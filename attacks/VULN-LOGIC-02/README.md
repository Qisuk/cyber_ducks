# VULN-LOGIC-02 — Order state machine not enforced server-side

| | |
|---|---|
| **Flag** | `VULN-LOGIC-02` (code, no restart); second layer: DB guard `VULN-GUARD-05` |
| **Where** | store-api `POST /api/orders/{id}/cancel` → `IOrderCancellation` ([OrderCancellation.cs](../../src/Store.Api/Orders/OrderCancellation.cs)) |
| **Class** | Business-logic flaw |
| **Threats** | business-logic bypass; asset A6 (orders) |

## The weakness

The store UI hides the cancel button once an order ships, but the API performs **no state check** in
vulnerable mode — it cancels an order in **any** state, including shipped or delivered. The classic
"the button was hidden but the API still allowed it" bug.

## Business impact

Customers (or an attacker with a session) can cancel — and void the invoice of — orders that have
already shipped, causing fulfilment/accounting mismatches.

## Confirm it

```bash
bash attacks/VULN-LOGIC-02/check.sh
```

The probe places an order, has an employee (`eve`) ship it, then tries to cancel the **shipped**
order as the customer. (Needs eve's default seeded password.)

- **Vulnerable:** `200 OK` — the shipped order is cancelled.
- **Secure:** `409 Conflict` — only `placed`/`paid` orders can be cancelled.

**Defence in depth:** if DB guard `VULN-GUARD-05` (no cancel after shipping) is on, Postgres blocks
the cancel even when VULN-LOGIC-02 is vulnerable — a second layer. The probe notes this.

## Observe in telemetry

The cancel span tags `defence.VULN-LOGIC-02`. Signature: a successful `POST /api/orders/{id}/cancel`
for an order whose status was `shipped`/`delivered`. See
[detections/VULN-LOGIC-02.md](../../detections/VULN-LOGIC-02.md).

## Fix

Flip `VULN-LOGIC-02` to **secure** (and keep `VULN-GUARD-05` on). Enforce the state machine on the
server; never rely on the UI hiding an action.

# VULN-GUARD-01…05 — Database guards (defence in depth)

| | |
|---|---|
| **Flags** | `VULN-GUARD-01` … `VULN-GUARD-05` (data; toggled via `set_guard()`) |
| **Where** | Postgres constraints/triggers ([03-guards.sql](../../infra/postgres/init/03-guards.sql)); panel "Database guards" |
| **Class** | Defence in depth (data integrity) |

## The idea

The guards are a **second layer** under the application checks: toggleable Postgres constraints and
one trigger. Even if an app-level defence is off or bypassed, the database can still refuse invalid
data. They start **off** (vulnerable) so the app flaws are reachable; turning a guard on shows the
data layer catching what the app missed.

| Guard flag | DB rule | Catches (app flaw) |
|---|---|---|
| VULN-GUARD-01 | `order_items.quantity > 0` | negative/zero quantity (no app check) |
| VULN-GUARD-02 | `order_items.unit_price >= 0` | negative price (needs [VULN-LOGIC-01](../VULN-LOGIC-01/) vulnerable to reach the DB) |
| VULN-GUARD-03 | `invoices.amount_due >= 0` | negative invoice total (from a negative-priced order) |
| VULN-GUARD-04 | `payments.amount > 0` | non-positive payment amount |
| VULN-GUARD-05 | trigger: no cancel after ship | cancelling a shipped order (see [VULN-LOGIC-02](../VULN-LOGIC-02/)) |

## Confirm it (GUARD-01, directly reachable)

The checkout does **not** validate quantity, so only the DB guard can stop a negative quantity.

```bash
bash attacks/VULN-GUARD-01/check.sh
```

- **Guard off (vulnerable):** an order with `quantity -1` is stored — bad data at rest.
- **Guard on (secure):** the insert is rejected (`500`, guard violation in the store-api logs).

Toggle `order_items_quantity_positive` under **Database guards** in Range Control to compare.

The other guards are shown via their partner flaws: GUARD-05 by [VULN-LOGIC-02](../VULN-LOGIC-02/)
(cancel after ship), and GUARD-02/03 by placing a negative-priced order while
[VULN-LOGIC-01](../VULN-LOGIC-01/) is vulnerable.

## Observe in telemetry

A guard rejection surfaces as a `500` on the write path with a Postgres error naming the guard
(e.g. `guard orders_no_cancel_after_ship…` or a constraint violation) in the Npgsql span / store-api
logs. See [detections/VULN-GUARD.md](../../detections/VULN-GUARD.md).

## Fix / use

Guards are themselves the hardening. Keep them **on** as a backstop even when the app checks are
secure — belt and braces. `set_guard()` is an allow-listed `SECURITY DEFINER` function callable only
by `range_control_owner`, so the panel can toggle them without exposing arbitrary DDL.

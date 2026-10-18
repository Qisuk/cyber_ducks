# Detect VULN-GUARD-01…05 — guard violations and bad data at rest

Two angles: **violations** (a guard is on and blocked a bad write) and **bad data at rest** (a guard
is off and invalid rows exist).

## Guard violations (Loki / LogQL)

When a guard is on, a blocked write is a `500` whose Postgres error names the guard:

```
{service_name="store-api"} |~ "(?i)guard |violates check constraint|order_items_quantity_positive|orders_no_cancel_after_ship"
```

A burst of these means something is repeatedly trying to write invalid data — the app check above is
off or being bypassed.

## Bad data at rest (SQL, when guards are off)

```sql
SELECT 'qty<=0'      AS issue, count(*) FROM order_items WHERE quantity <= 0
UNION ALL SELECT 'price<0',    count(*) FROM order_items WHERE unit_price < 0
UNION ALL SELECT 'invoice<0',  count(*) FROM invoices    WHERE amount_due < 0
UNION ALL SELECT 'payment<=0', count(*) FROM payments    WHERE amount <= 0
UNION ALL SELECT 'cancel-after-ship', count(*) FROM orders WHERE status='cancelled' AND shipped_at IS NOT NULL;
```

Any non-zero count is invalid data the guards would have blocked.

## Which guards are on? (panel / SQL)

```sql
SELECT guard, enabled FROM guard_status;   -- as range_control_owner
```

## Phase 8 alert idea

Alert on guard-violation log lines (someone is hammering invalid writes), and run the bad-data query
as a scheduled integrity check. Keep guards on in the hardened profile as a backstop.

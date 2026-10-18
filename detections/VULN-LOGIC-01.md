# Detect VULN-LOGIC-01 — under-priced order lines

**Signature:** an order whose stored line price is **below the catalogue price** for that product —
the stored total does not equal `sum(quantity × catalogue price)`.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-LOGIC-01 = "vulnerable" }
```

## Mismatched total (post-hoc query, SQL)

The clearest detection is in the data: order lines priced under catalogue.

```sql
SELECT oi.order_id, p.name, oi.unit_price, p.price AS catalogue_price
FROM order_items oi JOIN products p ON p.id = oi.product_id
WHERE oi.unit_price < p.price;
```

A real pipeline would emit this as a metric (count of under-priced lines) rather than an ad-hoc
query.

## Order placement spans (Tempo)

```
{ name = "POST /api/orders" }
```

Inspect for suspicious totals (e.g. multi-item orders totalling pennies).

## Phase 8 alert idea

Metric: count of order lines where `unit_price < catalogue price`, alert on `> 0`. Or validate at
write time and reject — which is exactly what the secure path does.

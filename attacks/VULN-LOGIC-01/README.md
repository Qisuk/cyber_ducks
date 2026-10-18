# VULN-LOGIC-01 — Checkout trusts the client-supplied price

| | |
|---|---|
| **Flag** | `VULN-LOGIC-01` (code, no restart) |
| **Where** | store-api `POST /api/orders` → `IOrderPricing` ([OrderPricing.cs](../../src/Store.Api/Orders/OrderPricing.cs)) |
| **Class** | Business-logic flaw (OWASP API & web) |
| **Threats** | TH2; assets A13/A14 (revenue, invoices) |

## The weakness

Each checkout line carries a `unitPrice`, and the vulnerable path stores the order using that
client value. The basket lives in an editable cookie, so a shopper can set any price — buy a £49.99
duck for 1p. The server never recomputes from the catalogue.

## Business impact

Direct revenue loss; every order total and invoice can be understated to near zero.

## Confirm it

```bash
bash attacks/VULN-LOGIC-01/check.sh
```

The probe places an order for a £7.49 product with `unitPrice` `0.01` and reads back the stored
total. (It creates a real test order — `docker compose down -v` resets.)

- **Vulnerable:** stored total is £0.01.
- **Secure:** stored total is £7.49 (server recomputed from the catalogue).

## Observe in telemetry

The `POST /api/orders` span tags `defence.VULN-LOGIC-01`. Signature: an order whose line
`unitPrice` is **below the catalogue price** for that product — a stored total that does not match
`sum(quantity × catalogue price)`. See [detections/VULN-LOGIC-01.md](../../detections/VULN-LOGIC-01.md).

## Fix

Flip `VULN-LOGIC-01` to **secure**: the secure `IOrderPricing` ignores the client price and uses the
current catalogue price. Never trust client-supplied prices, totals or discounts — recompute
server-side.

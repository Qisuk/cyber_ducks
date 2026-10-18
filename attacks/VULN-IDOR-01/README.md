# VULN-IDOR-01 — Broken object-level authorisation on orders

| | |
|---|---|
| **Flag** | `VULN-IDOR-01` (code, no restart) |
| **Where** | store-api `GET /api/orders/{id}` → `IOrderAccess` ([OrderAccess.cs](../../src/Store.Api/Orders/OrderAccess.cs)) |
| **Class** | OWASP API1:2023 Broken Object Level Authorization (IDOR) |
| **Threats** | TH4 (see [threat-model.md](../../docs/threat-model.md)); assets A1 (customer PII), A6 (orders) |

## The weakness

An order is fetched by its id alone. Any **logged-in** customer can read **any** order — including
another customer's name, delivery address, items and invoice — by changing the id in the URL. The
same `IOrderAccess` also governs cancel, so the gap is not read-only.

Vulnerable path returns the order by id; secure path also requires `o.UserId == currentUserId`.

## Business impact

Mass disclosure of customer personal data by walking sequential order ids. For a UK retailer that is
a personal-data breach (UK GDPR) — exactly the premise of the wargame scenario *Other people's
ducks*.

## Confirm it

Needs two seeded accounts. `bob@duckstore.test` owns order **1004**; we read it as `alice`.

```bash
bash attacks/VULN-IDOR-01/check.sh
```

- **Vulnerable:** alice receives `200 OK` with bob's order — his shipping address and items.
- **Secure:** alice receives `404 Not Found` (the order is not hers, so it does not exist for her).

By hand:

```bash
TOKEN=$(curl -s -XPOST http://localhost:8081/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"alice@duckstore.test","password":"password1"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8081/api/orders/1004 \
  -H "Authorization: Bearer $TOKEN"     # 200 vulnerable, 404 secure
```

## Observe in telemetry

The store-api request span carries `defence.VULN-IDOR-01 = vulnerable|secure` and a
`defence.resolved` event (service `IOrderAccess`). The tell-tale **attack** signature is one
customer (one JWT / session) fetching **many distinct** `/api/orders/{id}` values in a short window
— normal customers read a handful of their own. See [detections/VULN-IDOR-01.md](../../detections/VULN-IDOR-01.md).

## Fix

Flip `VULN-IDOR-01` to **secure** in Range Control (takes effect within seconds). The secure
`IOrderAccess` scopes every lookup to the caller's user id, so a non-owned id returns 404.
Defence in depth: object-level checks belong in the API, never only in the UI.

# VULN-AUTHZ-01 — Broken function-level authorisation on staff endpoints

| | |
|---|---|
| **Flag** | `VULN-AUTHZ-01` (code, no restart) |
| **Where** | store-api `/api/staff/*` → `IStaffGuard` ([StaffGuard.cs](../../src/Store.Api/Staff/StaffGuard.cs)) |
| **Class** | OWASP API5:2023 Broken Function Level Authorization |
| **Threats** | CT13; assets A1 (customer PII), A6 (orders) |

## The weakness

The staff endpoints require a login, but the vulnerable guard treats **any** authenticated user as
staff. A customer who finds `/api/staff/orders` can read the whole fulfilment queue — every order,
with every customer's email and address — and `POST /api/staff/orders/{id}/ship` to mark orders
shipped. The store UI only hides the staff link; it does not stop a direct request.

## Business impact

A single customer account becomes a window onto all customers' data and can alter order state.
Broken function-level authorisation is consistently near the top of the OWASP API list.

## Confirm it

`alice` is a plain customer (role `customer`). The probe calls a staff-only endpoint as alice.

```bash
bash attacks/VULN-AUTHZ-01/check.sh
```

- **Vulnerable:** `200 OK` — alice gets the full staff fulfilment queue (other customers' emails).
- **Secure:** `403 Forbidden` — only `employee` / `admin` roles pass.

By hand:

```bash
TOKEN=$(curl -s -XPOST http://localhost:8081/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"alice@duckstore.test","password":"password1"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8081/api/staff/orders \
  -H "Authorization: Bearer $TOKEN"     # 200 vulnerable, 403 secure
```

## Observe in telemetry

The store-api span for `/api/staff/*` carries `defence.VULN-AUTHZ-01`. The signature is a request to
a `/api/staff/*` route whose caller's `role` claim is `customer` returning `200` — a customer
reaching a staff function. See [detections/VULN-AUTHZ-01.md](../../detections/VULN-AUTHZ-01.md).

## Fix

Flip `VULN-AUTHZ-01` to **secure**: the secure `IStaffGuard` requires the `employee` or `admin`
role, so customers get `403`. Enforce function-level authorisation on the server for every
privileged route; never rely on hiding the link.

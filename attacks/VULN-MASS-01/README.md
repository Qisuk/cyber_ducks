# VULN-MASS-01 — Mass assignment on profile update (privilege escalation)

| | |
|---|---|
| **Flag** | `VULN-MASS-01` (code, no restart) |
| **Where** | store-api `PUT /api/auth/profile` → `IProfileUpdate` ([ProfileUpdate.cs](../../src/Store.Api/Auth/ProfileUpdate.cs)) |
| **Class** | OWASP API6:2023 / A08 — Mass assignment |
| **Threats** | TH7; asset A17 (staff privileges) |

## The weakness

The update binds **every** field in the request, including `role`. The edit form only offers a
display name, but the API honours any extra field a client adds — so a customer can set their own
`role` to `admin`. Classic mass assignment: the server trusts the shape of the request.

## Business impact

Self-service privilege escalation. An admin account unlocks the staff fulfilment queue, shipping,
and (combined with other flags) the rest of the store's data.

## Confirm it

```bash
bash attacks/VULN-MASS-01/check.sh
```

The probe sends `{"displayName":"...","role":"admin"}` as alice, reads the role back, and (if it
worked) restores alice to `customer`.

- **Vulnerable:** alice's role becomes `admin`.
- **Secure:** the `role` field is ignored; she stays `customer`.

## Observe in telemetry

The `PUT /api/auth/profile` span tags `defence.VULN-MASS-01`. Signature: a profile-update request
whose **body contains a `role` field** (normal updates never send one), especially followed by that
user appearing on staff routes. See [detections/VULN-MASS-01.md](../../detections/VULN-MASS-01.md).

## Fix

Flip `VULN-MASS-01` to **secure**: the secure `IProfileUpdate` binds only the display name; `role`
is never taken from the request. Bind to an explicit allow-list of fields, never the whole entity.

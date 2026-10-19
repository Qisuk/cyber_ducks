# Detect VULN-HONEYPOT-02 — honeypot account touched (decoy admin)

**Signature:** **any** login attempt against the decoy account `administrator@duckstore.test`, or
**any** read of its order (#1099). No legitimate user ever signs in as it or reads its data, so a
single hit is a high-confidence incident — credential stuffing, admin enumeration, or IDOR /
order-id walking.

The account is seeded (user id 99) with a **NULL password**, so an attempt can never actually
authenticate — it only alarms. Its order (#1099, owned by user 99) is reachable only by reading an
order that isn't yours, i.e. the IDOR path ([VULN-IDOR-01](VULN-IDOR-01.md)).

> **Provisioned alert (critical).** `range-honeypot-user` in
> [range-alerts.yaml](../infra/grafana/alerting/range-alerts.yaml) fires on the first hit and carries
> `range_node: store-api`, so the GrafanaAlertBridge raises store-api on the wargame map. No
> threshold: there is no legitimate traffic to tune against.

## The hit (Loki / LogQL)

```
{service_name="store-api"} |~ `HONEYPOT LOGIN|HONEYPOT DATA`
```

Two markers, both from store-api:

- `HONEYPOT LOGIN attempt for decoy admin account email=… ip=…` — someone tried to sign in as the
  decoy admin (credential stuffing / targeted admin guess). Logged on every attempt, before the
  password check, so it fires whether or not the guessed password is "right" (it never is).
- `HONEYPOT DATA access: decoy order 1099 read by user <id> ip=…` — someone read the decoy's order
  by id. Because it isn't their order, they can only have reached it via IDOR / id-walking.

## Who, and how far (Loki)

```
{service_name="store-api"} |= `HONEYPOT LOGIN`                 # which client IPs probed the decoy admin
{service_name="store-api"} |= `HONEYPOT DATA` | pattern `HONEYPOT DATA access: decoy order <o> read by user <u> ip=<ip>`
```

Correlate the `user` id on a `HONEYPOT DATA` line with the session that walked the order ids — that
is the authenticated account the attacker is using to enumerate. Cross-reference its IP with the
`Login 401 spike` (VULN-AUTH-01) and the IDOR orders-burst to scope the campaign.

## Why two triggers

A decoy account is attractive in two ways and attackers reach it by two routes:

- **Login route** — they see `administrator@duckstore.test` (guessed, or dumped from the users table
  via SQLi) and try to sign in. The login marker fires.
- **Data route** — they walk `/api/orders/{id}` and hit #1099. The data marker fires.

Together they catch both credential attacks and data-walking with near-zero false positives.

## This is detection, not a bug

There is no "secure mode" — the decoy account is a detective control that is always on. Its value is
a near-zero false-positive alert that fires on an attacker's own behaviour. Whether defenders *see*
the alert is governed separately by the SIEM toggle.

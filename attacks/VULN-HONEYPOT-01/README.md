# VULN-HONEYPOT-01 — decoy server / deception tripwire

| | |
|---|---|
| **Flag** | `VULN-HONEYPOT-01` — detection-only (no vulnerable/secure toggle); the decoy is always watching |
| **Where** | `honeypot` service ([src/Honeypot](../../src/Honeypot)); bait in [App.razor](../../src/Store.Web/Components/App.razor), [robots.txt](../../src/Store.Web/wwwroot/robots.txt), proxy decoy routes, and a seeded order note |
| **Class** | Deception / detective control (not a vulnerability) |
| **Threats** | TH3, TH12; supports detection of exfiltration and reconnaissance |

## What it is

A **honeypot**: a decoy "internal admin / data export" server that no legitimate flow ever touches.
It is linked from the store only as **bait**, through three vectors:

1. **Hidden markup + robots.txt** — an off-screen link to `/internal-backup` and `robots.txt`
   `Disallow:` entries for `/partner-export/`, `/internal-backup`, `/admin-export/`. The classic
   canary: "hiding" a path in robots.txt only advertises it to a scanner.
2. **A fake partner-export reference** — a stale `TODO` comment in the page source pointing at
   `/partner-export/customers?token=…`, the kind of note an attacker reads in the HTML.
3. **Agent-reachable text** — a seeded order note with a "partner tracking" link to the honeypot, so
   a prompt-injected agent can be lured into fetching it (see [VULN-EGRESS-01](../VULN-EGRESS-01/)).

The proxy (Z2) routes the decoy paths to the honeypot, so following the bait from the store is
same-origin and seamless.

## Because nothing legitimate touches it, any hit is the signal

There is no threshold and no false-positive budget to worry about: a single connection is an
incident. Every request makes the honeypot log a loud `HONEYPOT HIT` line — method, path, client IP,
user-agent, and **the full query string** (where naive exfiltration lands, e.g. `?data=…`).

## Where the warning fires

One emission, surfaced through the range's normal detection pipeline:

- **Grafana alert (primary):** the `HONEYPOT HIT` log reaches Loki via the collector; the provisioned
  rule `range-honeypot-hit` (`range_detection: VULN-HONEYPOT-01`, `range_node: honeypot`,
  severity **critical**) fires immediately.
- **Wargame Ops Console map:** because the alert carries `range_node: honeypot`, the
  GrafanaAlertBridge raises the **honeypot** node to *compromised* on the live detected map.
- **Loki (durable hunt record):** the `HONEYPOT HIT` lines are queryable for the full request detail.

## Try it

```bash
# As an "attacker" poking around — any of these trips it:
curl http://localhost/internal-backup
curl "http://localhost/partner-export/customers?data=stolen"
```

Then watch Grafana → Alerting (rule *Honeypot connection*) fire, and the honeypot node light up on the
Ops Console map during an exercise. See [detections/VULN-HONEYPOT-01.md](../../detections/VULN-HONEYPOT-01.md).

## Fix / design

A honeypot is a control, not a bug — there is nothing to "fix". The lesson is **deception as
detection**: a well-placed decoy turns an attacker's own reconnaissance into a high-confidence alert,
with none of the noise of signature-based rules. Pair it with [VULN-EGRESS-01](../VULN-EGRESS-01/)
(the honeypot is the external target an exfiltration attempt reaches) and the SIEM toggle (which
decides whether defenders *see* the alert).

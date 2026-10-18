# Detect VULN-HONEYPOT-01 — honeypot connection (decoy touched)

**Signature:** **any** request to the `honeypot` service. Nothing legitimate ever connects to it, so
a single hit is a high-confidence incident — reconnaissance, a scanner following robots.txt, or an
exfil/ingest hop (see [VULN-EGRESS-01](VULN-EGRESS-01.md)).

> **Provisioned alert (critical).** `range-honeypot-hit` in
> [range-alerts.yaml](../infra/grafana/alerting/range-alerts.yaml) fires on the first hit and carries
> `range_node: honeypot`, so the GrafanaAlertBridge raises the honeypot node to **compromised** on
> the wargame map. No threshold: a honeypot has no legitimate traffic to tune against.

## The hit (Loki / LogQL)

```
{service_name="honeypot"} |= `HONEYPOT HIT`
```

Each line carries the method, path, **query string** (where naive exfil lands, e.g. `?data=…`),
client IP (via `X-Forwarded-For` through the proxy) and user-agent. Read the query string to see what
someone tried to smuggle out.

## Who reached it (Loki)

```
{service_name="honeypot"} |= `HONEYPOT HIT` | pattern `HONEYPOT HIT <method> <path> from <ip> ua=<ua>`
```

Correlate the client IP / user-agent with store-web and proxy access logs to see how they found the
bait (robots.txt crawl, the hidden link, the page-source reference, or an agent `fetch_reference`).

## If it came from the agent (Tempo)

```
{ name = "fetch_reference" && span.http.url =~ ".*honeypot.*" }
```

A `fetch_reference` span targeting the honeypot means the agent was lured into an egress hop — pair
with [VULN-EGRESS-01](VULN-EGRESS-01.md) (open egress) and [VULN-LLM-02](VULN-LLM-02.md) (notes
reaching the model).

## This is detection, not a bug

There is no "secure mode" — the honeypot is a detective control that is always on. Its value is a
near-zero false-positive alert that fires on an attacker's own reconnaissance. Whether defenders
*see* this alert is governed separately by the SIEM toggle.

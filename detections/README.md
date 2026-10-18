# Detections

One file per vulnerability ID: how to **find the activity in Grafana** once the telemetry stack is
up (`http://localhost:3000`, Explore). These are hunt queries and signatures, not yet provisioned
alert rules — turning the strongest ones into alerts is Phase 8.

**Provisioned alerts (Phase 8a):** the strongest signatures here are now live Grafana alert rules in
[`infra/grafana/alerting/range-alerts.yaml`](../infra/grafana/alerting/range-alerts.yaml) (folder
"Cyber Ducks" → "range-detections"). Each carries a `range_detection` label (the VULN-ID) and a
runbook annotation pointing back here. Covered: VULN-AUTH-01 (login 401 spike), VULN-INFO-01 (5xx
spike), VULN-SQLI-01 (DB errors), VULN-WEBHOOK-01 (rejected webhook), VULN-GUARD (guard violation),
VULN-OAUTH-01 (OIDC correlation failures). Also added: the otel-collector now **redacts
card-number-like digit runs** from log bodies before storage (defence for VULN-PCI-01).

Data sources (otel-lgtm):
- **Tempo** (traces) — TraceQL. Spans carry the `defence.<ID>` tag set by each toggle, plus HTTP
  and DB attributes.
- **Loki** (logs) — LogQL. Container logs are labelled with `service_name` and the `range.zone`.
- **Prometheus** (metrics) — PromQL. HTTP server and runtime metrics.

Query attribute names can vary slightly with collector/OTel versions; adjust the tag/label names to
what you see in Explore. The signature described in words is the durable part.

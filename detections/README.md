# Detections

One file per vulnerability ID: how to **find the activity in Grafana** once the telemetry stack is
up (`http://localhost:3000`, Explore). These are hunt queries and signatures, not yet provisioned
alert rules — turning the strongest ones into alerts is Phase 8.

Data sources (otel-lgtm):
- **Tempo** (traces) — TraceQL. Spans carry the `defence.<ID>` tag set by each toggle, plus HTTP
  and DB attributes.
- **Loki** (logs) — LogQL. Container logs are labelled with `service_name` and the `range.zone`.
- **Prometheus** (metrics) — PromQL. HTTP server and runtime metrics.

Query attribute names can vary slightly with collector/OTel versions; adjust the tag/label names to
what you see in Explore. The signature described in words is the durable part.

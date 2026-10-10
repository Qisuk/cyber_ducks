# ADR-0005: OpenTelemetry + Grafana LGTM for logs, traces, metrics

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
We need a server that shows logs and node health, and that later supports detection/alerting for the
defence phase. It must fit in the RAM budget.

## Options
1. OpenTelemetry + Grafana LGTM (Loki, Grafana, Tempo, Prometheus/Mimir).
2. Elastic / OpenSearch stack (strong SIEM features, heavy on RAM).
3. Seq (excellent for .NET structured logs, logs only).
4. .NET Aspire dashboard (development tool, not for retention or alerting).

## Decision
Option 1. Start with the single `grafana/otel-lgtm` image. Splitting it into separate
services is a later Docker exercise. Add cAdvisor (container health) and postgres-exporter.
All .NET services emit OTLP (logs, traces, metrics) to the collector.

## Consequences
- Vendor-neutral instrumentation, so the backend can be swapped later.
- Security-specific tooling (Wazuh/Suricata) is deferred to Phase 8.

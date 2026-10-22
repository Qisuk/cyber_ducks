# Falco EDR (experimental, opt-in)

Host-level **behavioural** detection to complement the log-based Grafana alerts. Falco watches
syscalls (via a kernel driver) and fires on *behaviour* the logs can't see — a shell spawned in a
container, unexpected outbound connections, reads of backup/secret files.

It is **opt-in** via a Compose profile, so the default `docker compose up` does not start it.

## Enable

```bash
docker compose --profile edr up -d falco
docker compose logs -f falco
```

Disable again:

```bash
docker compose --profile edr stop falco && docker compose --profile edr rm -f falco
```

## ⚠️ Host support caveat

Falco needs a kernel driver (modern eBPF, legacy eBPF, or a kernel module). Support **varies by
host**, and on Docker Desktop / WSL2 it may need extra privileges or not load at all. Treat this as
experimental and verify on your machine:

- If Falco logs `driver loaded`/events, you're good.
- If it errors on the driver, try switching `FALCO_DRIVER` (see the service env in `compose.yaml`)
  between `modern_ebpf`, `ebpf`, and `kmod`, or run on a native Linux host.

## What fires

See [range-rules.yaml](range-rules.yaml): a shell in a `cyber-ducks-*` container, unexpected egress
from the app tier, and sensitive-file reads (`/backups/`, `*.env`, `RESTORE_KEY`). Events are JSON on
stdout and, because the service is labelled `range.logs=otlp`, flow to Loki.

## Wiring to the wargame map (follow-up)

To light a node on the detected map when Falco fires, add a Loki alert rule to
`infra/grafana/alerting/range-alerts.yaml` keyed on `RANGE-EDR` with `range_node` set to the affected
tier — the `GrafanaAlertBridge` then raises it exactly like the other detections. Left as a tuning
step because the node depends on which rule fired.

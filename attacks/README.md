# Attacks — vulnerability catalogue

One folder per vulnerability ID (e.g. `VULN-IDOR-01`). This is **teaching material for this local
range**, built around a loop:

**demonstrate → observe → detect → fix**

Each folder has:

- `README.md` — what the weakness is, which defence flag controls it, the business impact, how to
  confirm it (the request to send and what a vulnerable vs secure response looks like), the
  telemetry signature to hunt for, and the fix.
- `check.sh` — a small confirmation probe you run against **your own** range. It shows the current
  behaviour; flip the matching flag in Range Control to compare the secure path. These double as
  regression checks that each toggle still works.

Detections live alongside in [`../detections/<ID>.md`](../detections): the query to find the
activity in Grafana (Loki / Tempo / Prometheus) once the telemetry stack is up.

The index of every ID is [`../docs/vulnerabilities.md`](../docs/vulnerabilities.md).

## Scope and ground rules

- **Local range only.** The probes refuse to run against anything but `localhost`. All data is
  seeded and fake; all credentials are fake range values.
- These probes **demonstrate** a flaw (enough to confirm it and see it in telemetry). They are not
  general-purpose or reusable tooling, and they are deliberately minimal — the learning is in the
  observe/detect/fix steps, not in a payload.
- Flip flags in **Range Control** (`http://localhost:8090`, Defences tab). Most take effect within
  seconds; a few need a service restart (marked `restart` in the panel and in each README).

## Running a probe

```bash
# Start the range first:  docker compose up -d
bash attacks/VULN-IDOR-01/check.sh
```

The range must be running and reachable on its default published ports (store on
`http://localhost:8080`). The scripts need `curl` and a POSIX shell.

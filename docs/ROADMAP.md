# Roadmap & feature log

What the range can do, what was added in the latest round of improvements, and what is designed but
not yet built. For how to *run* an exercise, see [the runbook](../wargame/RUNBOOK.md).

## Added in this round

### CTF / engagement
- **Player leaderboard** — the Submit-a-flag form takes a **team name**, and a public
  `/leaderboard` page ranks teams by points, so the CTF self-drives without a facilitator tally.
- **20th flag — `ci-secret` (VULN-CICD-01)** — a leaked CI deploy config with a deploy token at
  `/.well-known/ci-deploy.yml`; the supply-chain / secrets-in-repo class.
- **One-click round reset** — Range Control → Flags has **Reset scoreboard** and
  **New flags (regenerate)** buttons (fresh per-deployment GUIDs, cleared board) without a teardown.

### Blue team
- **Detection-coverage view** — Range Control → Detection reads the live Grafana rules and shows
  which vulns have an alert vs are **silent** (caught only by reading telemetry).
- **After-action report export** — `GET /wargame/{id}/report.md` + a Download .md button: scenario,
  detected-vs-ground-truth, decisions by NIST phase, and the timeline, as Markdown.
- **Attack replay** — `scripts/attack-replay.sh {stuffing|sqli|idor|honeypot|webhook|all}` generates
  realistic attacker telemetry so a blue team can train solo without a live red team.

### AI & authoring
- **Rate-limit defence (VULN-RATELIMIT-01)** — a toggleable sliding-window cap (15/min per caller)
  on the assistant chat endpoint; vulnerable = unlimited (brute-force injection / abuse), secure =
  429 over the limit.
- **Deeper AI surface (RAG poisoning)** — a `get_product_reviews` agent tool over user-authored
  reviews; a poisoned review read on a victim's query can chain into other tools (governed by
  VULN-LLM-02). See [attacks/RAG-POISONING](../attacks/RAG-POISONING/README.md).
- **Scenario editor** — Range Control `/scenarios`: author/edit/delete wargame scenarios in a
  validated YAML editor, stored in the DB and merged into the catalogue (runnable like file
  scenarios).

### Content & ops
- **New scenarios** — `agent-data-exfil` (VULN-EGRESS-01 + VULN-LLM-02, exfil to the honeypot) and
  `privilege-escalation` (VULN-MASS-01 + VULN-AUTHZ-01, a customer self-promotes to admin).
- **Store onboarding** — `/rules` page: the game, how to get in, and rules of engagement.
- **Makefile** — `make up|stop|down|reset|rebuild|ps|logs|smoke|flags`.
- **Smoke test** — `scripts/smoke-test.sh` (health, surfaces, flag count, defences, model) so a fresh
  clone self-verifies.
- **Falco EDR (opt-in)** — host behavioural detection on the `edr` compose profile with range rules;
  experimental, see [infra/falco/README.md](../infra/falco/README.md).

## Designed, not yet built (next steps)

- **Silent-detection toggles** — turn the IDOR and AI-agent gaps into optional alerts (a request/tool
  span where `target customer != session sub`). Needs a Tempo/TraceQL-based alert or a span-metric,
  which Grafana unified alerting handles less directly than the existing Loki rules — prototype
  first.
- **Falco → map wiring** — a Loki alert on `RANGE-EDR` events with a `range_node`, so EDR findings
  raise a node like the other detections (left as tuning because the node depends on the rule).
- **Image digest pinning** — pin base images by digest for reproducible builds.

## Standing capabilities (reference)

Vulnerable store with ~34 toggleable defences; LGTM SIEM with provisioned alerts and the
detected-vs-ground-truth wargame map; honeypot server + honeypot user; toggleable agent egress;
Mailpit; password-reset interception; 20 per-deployment CTF flags with submission + scoreboard; five
plus two wargame scenarios with injects, speed control, autoplay and auto-seeded ground truth.

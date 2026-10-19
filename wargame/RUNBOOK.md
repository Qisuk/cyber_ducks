# Cyber Ducks — Wargame Facilitator Runbook

How to run an exercise on the range, and a complete worked example. Companion to
ADR-0008 (wargame & ops console) and the scenario files in `wargame/scenarios/`.

---

## 1. Roles

A wargame needs at minimum:

- **Facilitator (white cell)** — drives the Ops Console: starts the exercise, releases
  injects, sets ground truth, controls speed, and (for live-fire) flips defence flags.
  Sees everything. Does not play.
- **Blue team (players)** — the incident-response roles the injects are addressed to:
  incident commander, tech lead, security analyst, DPO/legal, comms, exec. They
  investigate in Grafana, decide, and log decisions in the Ops Console.
- **Red team (optional, live-fire)** — operates Kali and actually attacks the shop so the
  telemetry the blue team reads is *real*, not narrated. In a pure tabletop the facilitator
  narrates instead.

One person can facilitate and red-team at once for a small run.

---

## 2. The surfaces

| Surface | Address | Used by |
|---|---|---|
| The shop (attack target) | `192.168.56.1:8080` (LAN) / `127.0.0.1:8080` (host) | Red team / Kali |

> The LAN address is set by `STORE_BIND_IP` in `.env` (currently `192.168.56.1`, the
> VirtualBox host-only adapter) and `STORE_PORT` (`8080`). If you move the range to a
> different host-only network, update `STORE_BIND_IP` and point Kali at the new address.
| Grafana (logs, traces, metrics, alerts) | `127.0.0.1:3000` | Blue team (host) |
| Range Control / Ops Console (map, flags, wargame) | `127.0.0.1:8090` | Facilitator + blue team (host) |
| Mailpit (intercepted mail) | `127.0.0.1:8025` | Blue team (host) |

The three defender tools bind to `127.0.0.1` on the host only — the Kali VM cannot reach
them. Attacker on the LAN, blue team on the host.

### Logging in (important gotcha)

The store offers two ways to sign in, and they do **not** behave the same across addresses:

| Access via | Local login (email + password) | Google / Meta social login |
|---|---|---|
| `localhost:8080` (host) | ✅ works | ✅ works |
| `192.168.56.1:8080` (LAN / Kali) | ✅ works | ❌ fails — `redirect_uri` is pinned to `localhost` |

Social login (Google/Meta) is deliberately registered only for `http://localhost:8080` in
Keycloak, so hitting the Google/Meta buttons from the LAN IP returns a Keycloak 400
(`Invalid parameter: redirect_uri`). That is expected, not a fault.

**Practical rule for players on the LAN IP:** use a **local persona** to sign in, which
unlocks "Ask the Duck" (the AI assistant) and checkout:

| Persona | Email | Password | Role |
|---|---|---|---|
| Alice | `alice@duckstore.test` | `password1` | customer (weak pw) |
| Bob | `bob@duckstore.test` | `letmein` | customer (weak pw) |
| Eve | `eve@duckstore.test` | (see `.env`) | staff |
| Mallory | `mallory@duckstore.test` | (see `.env`) | admin |

For anything that exercises the **Google/Meta flow** (the VULN-OAUTH scenarios), run it from
the host via `http://localhost:8080`, not the LAN IP. "Can't see the chatbot" and "checkout
doesn't work" almost always mean *not signed in* — both pages are `[Authorize]`.

### The AI assistant is high-latency — widen tool timeouts

"Ask the Duck" is answered by a local LLM (`qwen3:4b-instruct`) running on **CPU**. A reply
routinely takes tens of seconds, and the first call after a restart can take ~90s while the
model loads. The server waits patiently (store-web → agent is 330s, agent → model 300s) and a
browser waits minutes, so from the host it just feels slow. But **automated tooling caps its
read timeout far lower** and will report the assistant as broken when it is only slow:

- **ZAP** defaults to a **20s** read timeout → `/assistant/send` fails with *"Failed to read…
  within 20 seconds."* Raise it: **Tools → Options → Network → Connection → "Timeout in secs"
  → 120** (older builds: Options → Connection). 120s is well under the 330s server ceiling.
- **curl:** `--max-time 120`. **Python requests:** `timeout=120`. Any scanner/fuzzer: widen
  the per-request timeout before pointing it at the agent.
- **Warm it once** before a run — send one assistant message and let it finish; the model then
  stays resident for 24h and later replies are quicker (still often >20s).

Lesson for the agent scenario: a scanner left at default timeouts will miss the
prompt-injection paths entirely, calling the AI tier "unavailable" when it is just slow.

---

## 3. Two ways to run

### Tabletop (facilitator-paced)
Discussion-led. The facilitator releases injects manually and the team reasons about them.
Telemetry may be pre-generated or narrated. Good for decision-making, comms, and legal/DPO
practice. Use the speed control only if you want a self-advancing clock in the background.

### Live-fire (red team attacks for real)
Red team runs the attack from Kali so Grafana and the map light up from genuine traffic.
This is where "unprotected vs protected" (below) belongs. The injects still frame the
incident, but the blue team's evidence is real.

### Auto-run (demo / solo)
"Auto-run" on exercise creation starts the exercise, applies scenario ground truth, and lets
the **ExerciseClock** release injects on a scaled clock (2×…100×). Use it for an unattended
demo or to rehearse a scenario solo.

---

## 4. Unprotected vs protected (the core A/B drill)

Every vuln has a flag with a `vulnerable` and a `secure` implementation (see
`AddDefence<…>` / `IsSecure`). Flipping it in Range Control swaps real behaviour live via
Postgres LISTEN/NOTIFY — no redeploy.

Run the **same attack twice**:

1. **Unprotected** — flag left `vulnerable` (default). Attack succeeds. Record what the blue
   team can see.
2. **Protected** — flip the flag to `secure`, repeat the identical attack. It fails or
   bounces; in several scenarios the *rejection* is what finally raises an alert.

**Where "what the defender sees" comes from:**
- **Grafana** — each request's trace is tagged `defence.VULN-XXX = vulnerable|secure`, plus
  logs/metrics. Provisioned alert rules fire on attack signatures.
- **Range Control map** — `GrafanaAlertBridge` polls Grafana and raises the matching node on
  the **detected layer** — *only when SIEM-01 is `secure`*. With SIEM-01 off the map stays
  dark though the attack worked (the "blind defender" lesson).
- **Mailpit** — reset mail / leakable tokens for the password-reset scenario.

> "No alert" ≠ "no incident." Webhook-fraud and agent-prompt-injection are silent in
> telemetry while vulnerable — detection comes from reconciliation or hand-reading spans.

**Always restore flags to their prior mode after a drill**, and delete/mutate-back any test
data you created, so the next run starts clean.

---

## 5. Running an exercise — the mechanics

In Range Control (`127.0.0.1:8090`):

1. **Wargames** page → pick a scenario → **Create**. Optionally tick **Auto-run** and a pace.
2. Open the **Exercise Console**. You get: the inject timeline, the dual-layer map
   (detected vs ground truth), speed control, and the decision log.
3. **Set ground truth** at the start (facilitator-only layer) — "Apply scenario ground
   truth" seeds it from the scenario's `ground_truth:` block. Keep it hidden from players
   until the debrief; it's what you compare their *detected* picture against.
4. **Release injects** as the team is ready (manual) or let the clock release them
   (autoplay). You can also **add / edit / delete timeline messages** to improvise.
5. Players **log every decision and its reason** in the console.
6. **Debrief**: reveal ground truth, compare to what detection actually showed, walk the
   `debrief_questions`.

---

## 6. Worked example — "Other people's ducks" (IDOR), live-fire

Scenario `customer-data-leak`. Simplest to run. ~20 min compressed, or the full 90-min
tabletop. Vuln: **VULN-IDOR-01** — a logged-in customer can read *any* order by id because
object-level authorisation is missing.

### Before you start
- Range healthy (`docker compose ps` all healthy on the host).
- Decide SIEM-01 state. For the first pass leave it **secure** (default) so the map lights
  up — you want players to see detection work. (Run it again with SIEM-01 off later to teach
  the blind-defender point.)
- Kali can reach `192.168.56.1:8080` (the `STORE_BIND_IP` host-only address).

### Facilitator setup (Ops Console)
1. Wargames → `customer-data-leak` → **Create** (tabletop, no auto-run for a facilitated run).
2. Exercise Console → **Apply scenario ground truth**. This seeds:
   `store-api = compromised` (IDOR), `postgres = suspected` (data read, not altered).
   Do **not** show players.
3. Have the map (detected layer) and decision log on the shared screen.

### Timeline

| T+ | Facilitator action | Red team (Kali) | What the blue team should see |
|---|---|---|---|
| 0 | Release **I1** (support ticket: "that isn't my order") | — | Nothing in telemetry yet — just a report. Triage: is it real? |
| ~2 | — | Log in as a normal customer, grab the session cookie/JWT, then walk order ids:  `for i in $(seq 1000 1300); do curl -s -b cookies.txt http://192.168.56.1:8080/api/orders/$i -o /dev/null -w "%{http_code} $i\n"; done` | In **Grafana**: a burst of `GET /api/orders/{id}` from **one client**, hundreds of distinct ids in minutes, mostly `200`. Normal customers fetch a handful of their *own*. Traces tagged `defence.VULN-IDOR-01 = vulnerable`. |
| ~10 | Release **I2** (on-call: odd traffic on orders API) | (let it finish) | Confirm scope from traces: which ids, which client, which window. This *is* the evidence; preserve it before changing anything. |
| ~20 | Release **I3** (social media post) | — | Pressure. Comms drafts a holding line — only what's confirmed. |
| ~30 | Release **I4** (DPO: is this reportable?) | — | Awareness time = when you first could reasonably know (the ticket? the traffic?). Count affected = distinct ids actually returned `200` to the attacker. |
| ~40 | Release **I5** (MD: do we take the shop down?) | — | Recommendation: contain while up (flip the flag) vs go offline before the sale. Trade-off logged. |
| ~50 | Release **I6** (journalist) | — | Comms consistency check. |
| ~55 | Release **I7** (fix ready — does it hold?). **Now flip VULN-IDOR-01 → secure** in Range Control. | Re-run the same id-walk from Kali | In **Grafana**: identical requests now return `403`/`404`, traces tagged `defence.VULN-IDOR-01 = secure`. The IDOR no longer works. Verify the fix *held* and watch for the attacker returning. |
| ~80 | Release **I8** (board: lessons learned) | — | One-page writeup. |
| debrief | **Reveal ground truth.** | — | Compare: did the detected map (store-api lit from the orders-traffic signal) match `store-api=compromised, postgres=suspected`? Where was the gap, and why? |

### After
- Flip **VULN-IDOR-01 back to `vulnerable`** (its prior default) unless you're keeping it
  secure deliberately.
- End/delete the exercise if it was a test run.
- No customer data is real, but if the red team mutated anything, restore it.

### The unprotected-vs-protected contrast in one screen
Run the id-walk once with the flag vulnerable (200s, data comes back) and once secure
(403/404, nothing). Put the two Grafana trace lists side by side — same request, tag flips
from `vulnerable` to `secure`, outcome flips from leak to blocked. That single comparison is
the whole teaching point of the range in miniature.

---

## 7. Defender's Grafana guide — spotting and investigating attacks

Grafana is at `http://localhost:3000` (anonymous admin is on by default). It's a Grafana
LGTM stack: **Loki** (logs), **Tempo** (traces), **Prometheus** (metrics). Two jobs: *spot*
an attack, then *investigate* it.

### (a) Spotting — passive monitoring

**Alerting is the first alarm.** **Alerting → Alert rules.** Eight rules are armed; each trips
on a real attack signature and (when SIEM-01 is secure) raises the matching node on the
Range Control detected map:

| Alert | Fires on | Lights node | Attack |
|---|---|---|---|
| Login 401 spike | rate of `401` on `/api/auth/login` | store-api | credential stuffing / brute force (VULN-AUTH-01) |
| store-api 5xx spike | rate of `5xx` from store-api | store-api | error probing (VULN-INFO-01) |
| store-api database errors | logs `npgsql \| syntax error` | store-api | **SQL injection** (VULN-SQLI-01) |
| Payment webhook rejected | logs `rejected payment webhook` | store-api | forged webhook (VULN-WEBHOOK-01) |
| DB guard violation | `violates check constraint` | postgres | invalid write blocked (VULN-GUARD) |
| OIDC correlation failures | `correlation failed \| nonce \| state` | store-web | login CSRF / replay (VULN-OAUTH-01) |
| **Honeypot connection** | `HONEYPOT HIT` | honeypot | decoy touched — **critical** (VULN-HONEYPOT-01) |

> Some attacks are silent by design. **IDOR** and the **agent prompt-injection** scenario have
> *no* alert — a fully working attack shows nothing in Alerting. "No alert" ≠ "no incident".

**The "Range health" dashboard** (Dashboards → Range health). Eyeball the golden signals even
when nothing alerts: request rate, error rate (4xx/5xx), latency, and the error-logs panel.
IDOR, which has no alert, is *spotted* here as a burst of `GET /api/orders/{id}` across
hundreds of distinct ids from one client — normal customers fetch a handful of their own.

### (b) Investigating — the Loki → Tempo pivot

Open **Explore** (compass icon) and drill:

**1. Logs (Loki).** Scope to the service + time window and grep for the signature:
```logql
{service_name="store-api"} |~ `(?i)npgsql|syntax error`     # SQLi probes
{service_name="store-api"} |= `/api/orders/`                 # IDOR id-walk: one client, many ids
{service_name="honeypot"}  |= `HONEYPOT HIT`                 # decoy: logs client IP, path, UA
```
Read off **which client, which window, which targets** — that is your scope.

**2. Traces (Tempo).** This range's superpower: every request span carries the defence
decision as a searchable tag. In Tempo search, query by attribute:
- `defence.VULN-IDOR-01 = vulnerable` → every request that actually ran the vulnerable path.
- Span attributes give the route, status, client, and the customer/order id — so you can size
  **real impact** (ids actually returned `200` to the attacker vs. merely attempted).
- Click a Loki log line's trace id to jump straight to its full trace.

**3. Scope & evidence — before anyone flips a flag.** Capture the attacker's client, the time
window, the exact ids/rows touched, and whether sensitive data was actually returned. That is
what the DPO/legal injects demand (how many customers, what data, when did we become aware).

### The 30-second loop (IDOR)

1. **Spot:** Range health shows a spike of `/api/orders/*` from one client (no alert — the lesson).
2. **Logs:** `{service_name="store-api"} |= `/api/orders/`` → one client, hundreds of ids, mostly `200`.
3. **Traces:** search `defence.VULN-IDOR-01 = vulnerable`, read the order ids vs. the session's
   customer id → confirm it is reading *other* customers' orders.
4. **Decide:** flip VULN-IDOR-01 → secure, re-run, confirm the same requests now return
   `403/404` and the trace tag flips to `secure`.

Every vuln has a detection doc with its exact signal and query in `detections/`
(`detections/VULN-SQLI-01.md`, `VULN-AUTH-01.md`, …) — worth having open per scenario.

---

## 8. Scenario → vuln → detection cheat-sheet

| Scenario | Primary vuln(s) | Does an alert fire? |
|---|---|---|
| `customer-data-leak` (IDOR) | VULN-IDOR-01 | Orders-traffic signal raises store-api |
| `sql-injection-breach` | VULN-SQLI-01 (+PWD-01, PCI-01) | DB-errors alert fires *if* injection traffic is run |
| `credential-stuffing` | VULN-AUTH-01 (+SESSION-01) | "Login 401 spike" raises store-api |
| `payment-webhook-fraud` | VULN-WEBHOOK-01 (+LOGIC-01) | **Silent while vulnerable** — only fires after the fix, when forgeries start bouncing |
| `agent-prompt-injection` | VULN-LLM-01 + VULN-MCP-01 | **No alert by design** — found by disclosure + reading agent spans (the detection-gap lesson) |

Pick a loud one (IDOR, SQLi, credential-stuffing) to teach detection; pick a silent one
(webhook, agent) to teach that monitoring gaps are themselves a finding.

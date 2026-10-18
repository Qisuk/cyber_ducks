# VULN-MCP-01 — Tool server calls APIs with a shared service account

| | |
|---|---|
| **Flag** | `VULN-MCP-01` (code) — **secure path built; vulnerable variant not yet wired** (planned) |
| **Where** | agent-tools → store-api calls ([CustomerTokenHandler.cs](../../src/AgentTools/CustomerTokenHandler.cs)) |
| **Class** | OWASP LLM06 (excessive agency) / LLM08 |
| **Threats** | CT53; assets A1 |

## Status

The **secure** behaviour is implemented and verified (Phase 6a): agent-tools forwards the customer's
JWT to store-api, so every tool call is scoped to that customer, and with no token there is no
access. The **vulnerable** variant — swapping the forwarded token for a shared service account — is
defined on the panel but **not yet wired**.

## The intended weakness

Vulnerable mode would have agent-tools call store-api as a broad **service account** instead of the
customer. Then a tool (or an over-broad future tool like `search_orders`) could read **any**
customer's data, and prompt injection could pull data across accounts — the tool gateway becomes a
confused deputy. Secure mode (current) calls as the customer, so store-api's own authorization
(IDOR/AUTHZ checks) still applies per request.

## How it is / will be confirmed

Secure path, today (Phase 6a verification): a tool call with no customer token returns an error — no
ambient access. When the vulnerable variant lands, a probe will show a tool returning another
customer's data via the service account.

## Observe in telemetry

The agent-tools → store-api span carries the DB/user identity and tags `defence.VULN-MCP-01`.
Signature: tool-originated API calls using a service account rather than the customer's token, or
returning rows for a `sub` other than the session's.

## Fix / design

Always call downstream APIs **as the customer** (forward their token); never a shared account. Keep
tools least-privilege and allow-listed (ADR-0009). Related: VULN-LLM-01 (identity from session).

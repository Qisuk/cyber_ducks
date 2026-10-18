# VULN-MCP-01 — Tool server calls APIs with a shared service account

| | |
|---|---|
| **Flag** | `VULN-MCP-01` (code) — **wired** (Phase 8) |
| **Where** | agent-tools → store-api calls ([CustomerTokenHandler.cs](../../src/AgentTools/CustomerTokenHandler.cs)) |
| **Class** | OWASP LLM06 (excessive agency) / LLM08 |
| **Threats** | CT53; assets A1 |

## Status

**Wired (Phase 8).** Secure (verified Phase 6a): agent-tools forwards the customer's JWT to store-api,
so every tool call is scoped to that customer, and with no token there is no access. Vulnerable: the
handler drops the customer token and logs in as a shared **service account** (a seeded persona,
configured in compose), calling store-api as that account for everyone.

## The weakness

In vulnerable mode agent-tools calls store-api as a broad **service account** instead of the customer,
so tool results reflect that account rather than the signed-in customer — and an over-broad future
tool, or prompt injection, could pull data across accounts. The tool gateway becomes a confused
deputy. Secure mode calls as the customer, so store-api's own authorization (IDOR/AUTHZ) still applies
per request.

## How it is confirmed

Secure: a tool call with no customer token returns an error — no ambient access. Vulnerable: sign in as
one customer and ask the agent for "my orders" — it returns the **service account's** orders, not the
caller's (cross-account via the confused deputy).

## Observe in telemetry

The agent-tools → store-api span carries the DB/user identity and tags `defence.VULN-MCP-01`.
Signature: tool-originated API calls using a service account rather than the customer's token, or
returning rows for a `sub` other than the session's.

## Fix / design

Always call downstream APIs **as the customer** (forward their token); never a shared account. Keep
tools least-privilege and allow-listed (ADR-0009). Related: VULN-LLM-01 (identity from session).

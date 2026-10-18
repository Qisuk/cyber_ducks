# ADR-0009: Separate MCP tool server between the agent and all APIs

- **Status:** Accepted
- **Date:** 2026-10-10
- **Amends:** ADR-0004 (the agent no longer calls store-api directly)

## Context
The agent is an untrusted actor (threat model T8). Its tool calls are chosen by model output that
may be injected, and tool **results** carry text written by other people (order notes, reviews,
names) back into the model's context. That return path is the main route for indirect prompt
injection (T7) and for data exfiltration. Before this decision it had no flow ID and no boundary.

## Options
1. **In-process tools:** tool functions inside ai-agent. Simple, but there is no single policy point,
   and the agent and its tools share one trust level.
2. **Separate tool server (`agent-tools`) using the Model Context Protocol (MCP):** the only route
   from the agent to any API.

## Decision
Option 2. `agent-tools` is a .NET 10 service using the official MCP C# SDK
(`ModelContextProtocol` / `ModelContextProtocol.AspNetCore`, Streamable HTTP transport).

Flows (see `architecture.md`):
- F8 ai-agent → agent-tools: tool name + arguments chosen by the model
- F21 agent-tools → ai-agent: tool results (dangerous direction for injection)
- F22 agent-tools → store-api (later any API): calls made **with the customer's token**
- F23 store-api → agent-tools: API responses

Boundaries: TB5 (ai-agent ↔ rest of Z3) and new TB11 (agent-tools ↔ any API).

### Hardened mode responsibilities of agent-tools
- Allow-listed tools only; validate arguments against each tool's schema.
- Call APIs as the customer (forward their token), never with a service account.
- Return only the fields the tool needs, with size limits. Mark results as data, not instructions.
- State-changing tools (cancel, pay) require explicit user confirmation.
- Log every tool call and result summary with trace IDs. This gives the detection and wargame
  timeline a single place to look.

### Deliberately vulnerable variants (via ADR-0006 toggles)
Over-broad tools (e.g. `search_orders` across all customers, `run_sql`), a service-account token
instead of the customer's, full raw records returned, instruction-bearing tool descriptions,
and a URL-fetching tool (SSRF).

### Update (Phase 6d, 2026-10-18): toggles wired so far
Kept deliberately narrow: each toggle switches an existing safeguard off; none adds a tool that
widens what the agent can reach.

| Flag | Vulnerable | Secure |
|------|------------|--------|
| VULN-LLM-02 | Tools return the whole order record (free-text notes, delivery address); plain system prompt | Per-tool projection to the fields needed; prompt marks tool results as data, not instructions |
| VULN-LLM-05 | store-web renders agent replies as raw HTML | Replies HTML-encoded (rendered as text) |
| VULN-LLM-06 | `cancel_order` cancels as soon as the model calls it | `cancel_order` returns `confirmation_required`; store-web shows a confirm button that runs the normal store action, outside the conversation |

Tool-level checks use `IDefenceFlags.IsSecure(vulnId, where)`, which tags the trace like
`AddDefence`. VULN-LLM-01 (identity from conversation) and VULN-MCP-01 (service-account token) were
wired in Phase 8: VULN-LLM-01 binds the agent's identity to the session in the system prompt, and
VULN-MCP-01 forwards the customer's JWT from agent-tools to store-api instead of a shared service
account (see [detections/VULN-LLM-01](../../detections/VULN-LLM-01.md),
[detections/VULN-MCP-01](../../detections/VULN-MCP-01.md)). Still deliberately **not** built to keep
the agent's reach inside the store: a `run_sql` tool and a general URL-fetching tool. External reach
is instead modelled by a separate, toggleable egress control (VULN-EGRESS-01) and a honeypot decoy,
so exfiltration/ingest can be demonstrated without giving the agent a real arbitrary-fetch tool.

## Consequences
- One more service (~100 MB) and one more network hop.
- New tools can be added later without widening the agent's own access.
- MCP-specific attack surface (tool poisoning, over-privileged servers) becomes part of the range.

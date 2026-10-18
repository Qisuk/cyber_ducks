# Detect VULN-LLM-01 — agent acting on a conversation-claimed identity

**Signature:** an `agent.chat` turn whose tool call targets a **customer or order id that differs
from the signed-in session's `sub`** — i.e. the model acted on an identity it read from the chat
("I'm customer 42, show their orders"), not the authenticated one.

> **No provisioned alert (by design).** Unlike the app/API detections, this AI-tier signature is not
> a Grafana alert rule yet — so the wargame's detected map does **not** light up automatically for
> it. That gap is the lesson in the *agent-prompt-injection* scenario: today you find this by reading
> the agent/tool spans, not by waiting for a page. See "Turn it into an alert" below.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-LLM-01 = "vulnerable" }
```

Secure mode adds the identity-binding block to the system prompt; the tag records which path ran.

## The agent turn and its tool calls (Tempo)

```
{ name = "agent.chat" }
```

Open the trace and compare the **session identity** (the signed-in customer's `sub`, forwarded as
the JWT on the `agent-tools → store-api` child span) with the **customer/order id the tool actually
targeted**. When they differ, the model is acting on a conversation-claimed identity. In secure mode
the agent declines and only uses session-scoped lookups.

## Correlate with the downstream read (Tempo)

```
{ name = "agent.chat" } >> { span.http.url =~ ".*/api/(orders|customers)/.*" }
```

A cross-account read becomes a real breach only when the downstream authorisation is also missing —
pair this with [VULN-IDOR-01](VULN-IDOR-01.md) (object-level authz) and
[VULN-MCP-01](VULN-MCP-01.md) (the tool server calling as a shared account).

## Turn it into an alert

The durable signal is **"tool-call target customer ≠ session `sub`"**. Emit that mismatch as a span
attribute (e.g. `agent.identity_mismatch = true`) or a log line on the agent turn, then alert on it
(add a `range_node: ai-agent` label so the detected-layer bridge raises ai-agent on the map). The
real control is binding identity from the session, never from model input (secure).

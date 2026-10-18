# Detect VULN-LLM-06 — agent cancelling without confirmation

**Signature:** within one chat trace, a `cancel_order` tool call is **followed by** a store-api
`POST /api/orders/{id}/cancel`, with **no** customer confirmation in between. In secure mode the
tool returns `confirmation_required` and the API cancel only happens later, from the store's confirm
button, in a **separate** request/trace.

## The agent turn (Tempo / TraceQL)

```
{ name = "agent.chat" && span.agent.tools_used =~ ".*cancel_order.*" }
```

Then read `agent.pending_actions`:
- secure mode → `pending_actions >= 1` **and no** `/api/orders/{id}/cancel` in the same trace
  (the cancel is deferred to the button);
- vulnerable mode → the same trace continues into agent-tools → store-api
  `POST /api/orders/{id}/cancel`.

## Is the defence off right now? (Tempo)

```
{ span.defence.VULN-LLM-06 = "vulnerable" }
```

## Correlation (Loki / LogQL)

ai-agent logs each turn with the tools used and pending-confirmation count:

```
{service_name="ai-agent"} |= "tools used: cancel_order"
```

A line showing `cancel_order` used but `pending confirmations: 0` means the tool acted without
asking — the vulnerable path.

## Phase 8 alert idea

A trace containing `cancel_order` → `POST …/cancel` with no intervening confirm event → alert.
Confirmation is an explicit, separate store action, so its absence in-trace is the tell.

# VULN-LLM-06 — Agent performs state-changing actions without confirmation

| | |
|---|---|
| **Flag** | `VULN-LLM-06` (code, no restart) |
| **Where** | agent-tools `cancel_order` tool ([OrderTools.cs](../../src/AgentTools/OrderTools.cs)); store-web confirm button ([Assistant.razor](../../src/Store.Web/Components/Pages/Assistant.razor)) |
| **Class** | OWASP LLM06:2025 Excessive Agency |
| **Threats** | CT28 (see [claude_threats.md] — local) |

## The weakness

The agent has a `cancel_order` tool. In vulnerable mode the tool cancels the order **as soon as the
model calls it** — so anything that steers the model into a cancel (a confused reply, or indirect
prompt injection via text in an order note the agent reads) changes order state with no human in the
loop. In secure mode the tool refuses to act: it returns `confirmation_required`, and the store
shows the customer a real **Confirm: cancel order #N** button that runs the normal, CSRF-protected
cancel endpoint — an action the model cannot click.

The order id for the confirm button comes from the **tool call's arguments**, not from the model's
reply text, so the model cannot substitute a different order.

## Business impact

Excessive agency: the model can take consequential actions on a customer's account. Combined with
indirect prompt injection (tool results carrying other people's text), an attacker's text could
drive actions the customer never asked for.

## Confirm it

This path runs through the LLM, so confirm it in the browser rather than with a script.

1. Sign in to the store (`http://localhost:8080`) as `alice@duckstore.test` / `password1`.
2. Open **Ask the Duck** and send: *"Please cancel my order 1014."*
3. Watch order 1014's status (My orders), or the DB.

- **Secure (VULN-LLM-06 secure):** the reply says to press the confirm button; a
  **Confirm: cancel order #1014** button appears under the reply; order 1014 stays `placed` until
  you click it.
- **Vulnerable:** the order is cancelled during the chat turn, with no button and no confirmation.

The deterministic, model-free version of this check is the xUnit test
`AgentDefenceTests.Secure_cancel_order_waits_for_the_customer_and_never_calls_the_store`
([AgentDefenceTests.cs](../../tests/CyberDucks.Tests/AgentDefenceTests.cs)) — it asserts the secure
tool never calls store-api, and the vulnerable tool does.

## Observe in telemetry

The `agent.chat` span tags `agent.tools_used` (includes `cancel_order`) and
`agent.pending_actions`. agent-tools records `defence.VULN-LLM-06` on the tool call. Signature: a
`cancel_order` tool call **followed by a store-api `POST /api/orders/{id}/cancel`** within the same
trace and **no** preceding customer confirmation event. See
[detections/VULN-LLM-06.md](../../detections/VULN-LLM-06.md).

## Fix

Flip `VULN-LLM-06` to **secure**. State-changing tools must not act on model output alone: they
return a pending action that the user confirms out-of-band. Keep agent tools least-privilege, and
treat tool results as data (see VULN-LLM-02).

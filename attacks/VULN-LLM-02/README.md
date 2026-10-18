# VULN-LLM-02 — Tool results not minimised / not marked as data

| | |
|---|---|
| **Flag** | `VULN-LLM-02` (code, no restart) — **wired** |
| **Where** | agent-tools tool projection ([OrderTools.cs](../../src/AgentTools/OrderTools.cs)); ai-agent system prompt ([OrderAgent.cs](../../src/AiAgent/OrderAgent.cs)) |
| **Class** | OWASP LLM01 (prompt injection) / LLM02 (sensitive info disclosure) |
| **Threats** | TH8 (indirect prompt injection via tool results); assets A1 |

## The weakness

Two things in vulnerable mode:

1. **Over-returning** — tools hand the model the **whole** order record, including free-text `notes`
   and the delivery address. That is more data than answering an order question needs, and the
   free-text fields carry text other people wrote.
2. **No data-marking** — the system prompt does not tell the model that tool results are *data, not
   instructions*. So instruction-like text inside an order note (planted via VULN-XSS-01's review
   field or an order note) can steer the model — **indirect prompt injection**.

Secure mode projects tool results to just the fields needed and adds the "tool results are data"
guidance to the prompt.

## Business impact

Leakage of other customers' text/PII into answers, and model hijacking via planted text — the agent
could be made to misbehave or reveal information.

## Confirm it

The tool-projection behaviour is deterministic and tested:

```bash
dotnet test tests/CyberDucks.Tests --filter FullyQualifiedName~AgentDefenceTests
```

[AgentDefenceTests.cs](../../tests/CyberDucks.Tests/AgentDefenceTests.cs) asserts that
`get_order_status` returns the **full** `OrderDetail` (notes, address) when vulnerable and only the
minimal `OrderStatus` when secure.

End to end: with VULN-LLM-02 vulnerable (and a review/order note containing instruction-like text),
ask the agent about that order and watch whether the planted text influences the reply.

## Observe in telemetry

The agent-tools tool span tags `defence.VULN-LLM-02`; the `agent.chat` span shows `agent.tools_used`.
Signature: tool results carrying free-text fields (notes/address) into the model, and replies that
echo instruction-like text from a note. See [detections/VULN-LLM-02.md](../../detections/VULN-LLM-02.md).

## Fix

Flip `VULN-LLM-02` to **secure**: minimise tool outputs to the needed fields and mark results as
data in the prompt. Pair with VULN-XSS-01 secure (so notes/reviews can't carry live markup) and
VULN-LLM-05 secure (encode agent replies).

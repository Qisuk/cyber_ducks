# Detect VULN-LLM-02 — over-returned tool data / indirect injection

**Signature:** tool results carrying free-text fields (order `notes`, delivery address) into the
model, and agent replies that echo instruction-like text planted in those fields.

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-LLM-02 = "vulnerable" }
```

## The agent turn (Tempo)

```
{ name = "agent.chat" && span.agent.tools_used =~ ".*get_order_status.*" }
```

In vulnerable mode the tool result includes `notes`/`shippingAddress`; in secure mode it does not.

## Injection attempts (Loki / LogQL)

Hunt the carrier fields for instruction-like text (also a VULN-XSS-01 signal):

```
{service_name="ai-agent"}   # correlate with order notes/reviews containing "ignore previous", "system:", URLs, etc.
```

Combine with the at-rest query from [VULN-XSS-01](VULN-XSS-01.md) over `product_reviews.body` and
`orders.notes`.

## Phase 8 alert idea

Alert when a tool result field that should be internal (notes/address) is present in model context,
and when replies contain text matching known injection phrases. The real control is minimising tool
output + marking results as data (secure).

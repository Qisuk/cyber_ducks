# Detect VULN-LLM-05 — markup in agent replies

**Signature:** an agent reply containing HTML/script markup, rendered raw on the chat page. Like
XSS-01, this is a **content** signal, not a request-shape one.

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-LLM-05 = "vulnerable" }
```

## Agent replies (Loki / LogQL)

ai-agent logs each turn; hunt replies that contain markup:

```
{service_name="ai-agent"} |~ "<\\s*(script|img|svg|iframe|b|i|a)\\b"
```

A reply containing markup, combined with VULN-LLM-05 vulnerable, means that markup rendered live in
the customer's browser.

## Phase 8 idea

Prevent, don't detect: encode agent output (secure VULN-LLM-05) and set a Content Security Policy on
the store. For monitoring, flag agent replies containing `<`/`>` in a context that should be plain
text.

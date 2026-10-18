# VULN-LLM-05 — Agent replies rendered as raw HTML

| | |
|---|---|
| **Flag** | `VULN-LLM-05` (code, no restart) — **wired** |
| **Where** | store-web chat rendering ([Assistant.razor](../../src/Store.Web/Components/Pages/Assistant.razor)) |
| **Class** | OWASP LLM05 — Improper output handling |
| **Threats** | XSS via model output; assets A1, A12 |

## The weakness

In vulnerable mode the "Ask the Duck" page renders the agent's reply as **raw HTML**
(`@((MarkupString)...)`). Model output is untrusted — the model can be steered (directly, or via
indirect injection from a tool result, VULN-LLM-02) into emitting markup. Rendered raw, that markup
runs in the customer's browser: the LLM becomes an XSS vector. Secure mode renders replies as text
(encoded).

This is the agent-reply analogue of the stored XSS in reviews (VULN-XSS-01) — same class, different
source.

## Business impact

XSS in the chat surface: session theft or actions as the customer, triggered by the assistant's own
reply.

## Confirm it

The reply content comes from the LLM, so confirm it in the browser:

1. Sign in to the store and open **Ask the Duck**.
2. Coax the model into including a simple HTML tag in its reply (e.g. ask it to "reply with the word
   duck in bold HTML"), or rely on a tool result (a review/note, VULN-LLM-02 vulnerable) that
   contains markup.
3. **Vulnerable:** the tag renders (bold text / live element). **Secure:** the tag shows as text.

The rendering branch itself is in `Assistant.razor` (`_encodeReplies = Flags.IsSecure("VULN-LLM-05")`).

## Observe in telemetry

Server-side the chat POST is normal; the signature is in the rendered content (markup in an agent
reply). Best addressed by encoding, not detection. See [detections/VULN-LLM-05.md](../../detections/VULN-LLM-05.md).

## Fix

Flip `VULN-LLM-05` to **secure**: render agent output as text. Treat model output as untrusted;
encode it, and apply a Content Security Policy (Phase 8).

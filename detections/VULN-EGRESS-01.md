# Detect VULN-EGRESS-01 — agent egress to a non-store host (exfil / ingest)

**Signature:** a `fetch_reference` tool call whose target **host is not a store host**
(`store-api` / `idp` / `payment-stub`) — the agent reaching out to the honeypot, a pastebin, or any
external address to smuggle data out or pull untrusted data in.

> **No provisioned alert of its own** — but when the target is the honeypot, the hit lands as the
> critical [VULN-HONEYPOT-01](VULN-HONEYPOT-01.md) alert, which is the intended demonstration. The
> general signature below is the hunt query for any egress target.

## Is the control off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-EGRESS-01 = "vulnerable" }
```

Secure mode's `EgressGuardHandler` refuses non-store hosts; vulnerable mode lets the fetch out.

## The egress hop (Tempo)

```
{ name = "fetch_reference" }
```

Inspect the target host on the span. In secure mode a non-store host returns a `403 Blocked by egress
policy` (the handler short-circuits); in vulnerable mode the outbound GET completes. A completed fetch
to anything other than `store-api` / `idp` / `payment-stub` is the signature.

## Data smuggled in the URL (Loki — honeypot side)

If the egress target is the honeypot, the exfiltrated data is right there in the request:

```
{service_name="honeypot"} |= `HONEYPOT HIT` |~ `\?.*=`
```

## Correlate with the lure (Loki)

A planted link usually arrives via agent-reachable text (indirect injection):

```
{service_name="store-api"} |~ `(?i)http://|https://`   # URLs in order notes / reviews the agent ingests
```

Tie this to [VULN-LLM-02](VULN-LLM-02.md) (tool results carrying notes into the model) and
[VULN-HONEYPOT-01](VULN-HONEYPOT-01.md).

## Turn it into an alert

Alert on **any `fetch_reference` span whose host is not on the store allowlist**. The real control is
least-privilege egress (secure) plus the sealed agent network — neither alone load-bearing.

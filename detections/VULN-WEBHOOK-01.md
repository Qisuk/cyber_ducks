# Detect VULN-WEBHOOK-01 — forged payment webhook

**Signature:** a `POST /api/payments/webhook` that marks an invoice paid **without** a valid
`X-Payment-Signature`, or from an unexpected source address (not payment-stub).

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-WEBHOOK-01 = "vulnerable" }
```

## Rejections (Loki / LogQL)

In secure mode, forged calls are logged as rejected:

```
{service_name="store-api"} |= "Rejected payment webhook with an invalid signature"
```

Seeing these means someone is sending unsigned/mis-signed webhooks — the probe, or an attacker.
In vulnerable mode there is **no** such log line because nothing is rejected, which is itself the
problem.

## Source check (Tempo)

```
{ name = "POST /api/payments/webhook" }
```

Legitimate calls come from `payment-stub` on the `range` network. A webhook span whose client is
anything else is suspicious.

## Phase 8 alert idea

Alert on any webhook that lacks a valid signature (secure mode makes this a rejection to count), and
on webhook source addresses outside the expected provider. Network policy can also restrict who may
reach the webhook route.

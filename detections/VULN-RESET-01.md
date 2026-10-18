# Detect VULN-RESET-01 — weak password reset (brute force / enumeration)

**Signature:** many `POST /api/auth/forgot-password` requests (enumeration / spraying), or repeated
`POST /api/auth/reset-password` attempts against one account (guessing the 6-digit token), or a reset
completing from a different client than the one that requested it (interception).

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-RESET-01 = "vulnerable" }
```

Vulnerable issues a guessable token and leaks account existence; secure issues a strong single-use token.

## Reset requests (Loki / LogQL)

```
{service_name="store-api"} |= "/api/auth/forgot-password"
```

A spike across many distinct emails is enumeration/spraying. The `404` vs `200` split (vulnerable
only) is what an attacker uses to enumerate.

## Token brute force (Loki / Prometheus)

```
{service_name="store-api"} |= "/api/auth/reset-password"
```

Many attempts against one account in a short window = brute-forcing the 6-digit token. In secure mode
the 256-bit token makes this hopeless.

## Turn it into an alert

Alert on `forgot-password` rate per client and on `reset-password` failures per account over a
threshold. The real control is a strong single-use token + generic responses + rate limiting (secure).

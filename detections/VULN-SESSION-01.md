# Detect VULN-SESSION-01 — forged / unsigned JWTs

**The hard truth:** in vulnerable mode a forged token is *accepted silently*, leaving almost no
trace — which is exactly why signature verification must be on. Most detection value is in **secure**
mode (rejections) and in spotting anomalous claims.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-SESSION-01 = "vulnerable" }
```

## Rejections in secure mode (Loki / LogQL)

```
{service_name="store-api"} |~ "(?i)signature validation failed|invalid.?signature|malformed signature|alg.?none"
```

A burst of these means someone is presenting forged/unsigned tokens and being rejected — active
probing.

## Anomalous sessions (behavioural)

Because an accepted forgery is quiet, hunt its *effects*: a `sub` acting from many IPs, a token whose
`role` is `admin` for a user who is a `customer` in the database, or staff actions by an account
that never logged in. Correlate `user.id`/`role` on spans against the `users` table.

## Phase 8 alert idea

Alert on `alg:none` or signature-failure log lines, and on privileged actions whose token role does
not match the user's stored role. Keep tokens short-lived to limit a forged token's window.

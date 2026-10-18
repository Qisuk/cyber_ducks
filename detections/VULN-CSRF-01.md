# Detect VULN-CSRF-01 — cross-site form posts

**Signature:** a state-changing POST (profile, order, review) whose `Origin`/`Referer` is a
different site, or that lacks a valid anti-forgery token.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-CSRF-01 = "vulnerable" }
```

## Cross-origin posts (Tempo)

Inspect state-changing form routes and their request headers:

```
{ span.http.request.method = "POST" && span.http.route =~ "/account/.*|/orders/.*|/ducks/.*/review" }
```

A POST whose `Origin` header is not the store's own origin is the red flag. (Add `Origin`/`Referer`
as span attributes to make this queryable.)

## Rejections (secure mode)

When secure, missing/invalid tokens produce `400`s:

```
sum(rate(http_server_request_duration_seconds_count{http_response_status_code="400"}[5m]))
```

## Phase 8 alert idea

Alert on state-changing POSTs with a foreign `Origin`. The edge/WAF can enforce `Origin` checks;
the app enforces the anti-forgery token (secure VULN-CSRF-01).

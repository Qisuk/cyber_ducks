# Detect VULN-INFO-01 — stack traces in responses

**Signature:** a `500` **response body** that contains a stack trace / exception type. The status
code alone is normal; the leak is in the body.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-INFO-01 = "vulnerable" }
```

## Server-side errors (Loki / LogQL)

The exception is logged regardless of the flag, so the logs show the underlying 500s:

```
{service_name="store-api"} |= "Unhandled exception for"
```

A cluster of these (often correlated with `products/search` probing) means someone is forcing
errors — and in vulnerable mode each one hands them a stack trace in the response.

## 5xx rate (Prometheus / PromQL)

```
sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))
```

## Phase 8 alert idea

The response-body leak is best prevented, not detected: keep VULN-INFO-01 secure. For detection,
alert on a rising 5xx rate and investigate the correlated request pattern (often injection probing).

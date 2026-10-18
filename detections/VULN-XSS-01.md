# Detect VULN-XSS-01 — markup in review bodies

**Signature:** a review (or any free-text field) whose body contains HTML/script markup. Server-side
the POST looks normal, so detection is on **content**, not the request shape.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-XSS-01 = "vulnerable" }
```

## Markup at rest (SQL / data)

```sql
SELECT id, product_id, left(body, 80) FROM product_reviews
WHERE body ~* '<\s*(script|img|svg|iframe|b|i|a)\b' OR body LIKE '%<%>%';
```

Any hit is user-supplied markup — benign-looking tags and `<script>` alike. The same query over
order `notes` finds injection there too.

## Ingest-time check (Phase 8)

A content filter at review submission (or a WAF rule on the POST body) is the practical control.
Log/flag submissions containing `<` `>` in fields that should be plain text.

## Note

The real fix is output encoding (secure VULN-XSS-01) + a Content Security Policy. Detection is a
backstop: by the time markup is stored, a visitor may already have been hit.

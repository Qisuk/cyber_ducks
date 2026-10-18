# Detect VULN-SQLI-01 — injection in product search

**Signature:** `/api/products/search` requests whose **executed SQL** contains injection markers
(`UNION`, `--`, `OR 1=1`, stacked quotes), and/or a spike of database errors from that route.

## Executed SQL on the DB span (Tempo / TraceQL)

Npgsql client spans record the statement text. In vulnerable mode the injected text appears in it:

```
{ span.db.system = "postgresql" && span.db.statement =~ ".*UNION SELECT.*" }
```

Secure mode shows the fixed parameterised statement with a bound `@q` and no injected clauses.

## Is the defence off right now? (Tempo)

```
{ span.defence.VULN-SQLI-01 = "vulnerable" }
```

## Error spike (Loki / LogQL)

Payload development throws database errors (amplified by VULN-INFO-01's verbose errors):

```
{service_name="store-api"} |= "Npgsql" |~ "(?i)syntax error|exception"
```

A cluster of these correlated with `products/search` requests indicates someone probing the
parameter.

## Phase 8 alert idea

Any Npgsql span whose `db.statement` matches injection patterns → alert. Pairs with turning
VULN-INFO-01 secure (generic errors) so probing gets no feedback, and VULN-DBROLE-01 secure so a
successful injection reads far less.

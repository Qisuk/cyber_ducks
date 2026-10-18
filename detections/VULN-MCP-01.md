# Detect VULN-MCP-01 — tool server calling the API as a shared service account

**Signature:** an `agent-tools → store-api` call whose identity is the seeded **service account**
(the configured persona) rather than the signed-in customer — tool-originated API calls that all
resolve to the same `sub`/DB user regardless of who is chatting, or that return rows for a customer
other than the session's.

> **No provisioned alert (by design).** Like [VULN-LLM-01](VULN-LLM-01.md), this is an AI-tier
> signature with no Grafana alert rule yet, so the detected map does not raise automatically. You
> find it by reading the tool→API spans. See "Turn it into an alert" below.

## Is the defence off right now? (Tempo / TraceQL)

```
{ span.defence.VULN-MCP-01 = "vulnerable" }
```

Secure mode forwards the customer's JWT; vulnerable mode drops it and logs in as the shared service
account. The tag records which path ran on the `agent-tools` outbound call.

## The confused-deputy call (Tempo)

```
{ name =~ "GET|POST .*/api/.*" && span.service.name = "agent-tools" }
```

Inspect the identity the call carried into store-api (the JWT `sub` / DB user on the child span).
In secure mode it is the **customer** doing the chatting; in vulnerable mode it is the **service
account** (`bob@duckstore.test` by default) for *every* customer — the tell is the same identity
across sessions that belong to different signed-in users.

## Cross-account result (Tempo)

```
{ name = "agent.chat" } >> { span.service.name = "store-api" && span.db.user =~ ".*service.*" }
```

Ask the agent "show my orders" as one customer and watch the downstream span return the **service
account's** rows, not the caller's. That mismatch between session `sub` and returned owner is the
breach.

## Turn it into an alert

The durable signal is **"tool-originated API call whose identity is the service account, or whose
returned owner ≠ session `sub`."** Emit it as a span attribute / log line and alert on it (add a
`range_node: agent-tools` label so the detected-layer bridge raises agent-tools on the map). The
real control is always calling downstream as the customer, never a shared account (secure, ADR-0009).

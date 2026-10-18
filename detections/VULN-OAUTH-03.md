# Detect VULN-OAUTH-03 — social link on an unverified email

**Signature:** a `POST /api/auth/social` that links a federated identity to an existing account
where the provider's `email_verified` is false, or a social identity whose email matches a
pre-existing password account it never owned.

## Which mode is active? (Tempo / TraceQL)

```
{ span.defence.VULN-OAUTH-03 = "vulnerable" }
```

## The link event (Loki / LogQL)

```
{service_name="store-api"} |= "/api/auth/social"
```

Look for a social login that resolves to an existing user id while the incoming `email_verified`
claim is false — that is the takeover path. If audit logging (VULN-LOG-01) is on, a "social identity
linked" event with `email_verified=false` is the direct hit.

## In the data (SQL)

```sql
-- Social identities whose email collides with a different account's email:
SELECT ui.user_id, ui.provider, ui.email
FROM user_identities ui JOIN users u ON u.email = ui.email AND u.id <> ui.user_id;
```

## Phase 8 / hardening

Link only on verified emails (secure VULN-OAUTH-03). Alert on any social link with
`email_verified=false`, and on a federated identity binding to an account created by password.

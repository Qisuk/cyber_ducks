# VULN-SQLI-01 — SQL injection in product search

| | |
|---|---|
| **Flag** | `VULN-SQLI-01` (code, no restart) |
| **Where** | store-api `GET /api/products/search?q=` → `IProductSearch` ([ProductSearch.cs](../../src/Store.Api/Catalogue/ProductSearch.cs)) |
| **Class** | OWASP A03:2021 Injection |
| **Threats** | TH26; assets A1 (customer PII), A2 (credentials) |

## The weakness

The search text is concatenated straight into the SQL string:

```
... WHERE name ILIKE '%<q>%' OR description ILIKE '%<q>%' ...
```

A crafted `q` closes the string and changes the statement. The endpoint is **public** (no login),
and verbose database errors (VULN-INFO-01) make a payload easy to develop. The query returns six
columns (`id, sku, name, price, stock, image_url`), so a `UNION SELECT` of six values injects
attacker-chosen rows — and, in the same shape, rows read from other tables.

## Business impact

Read access to anything the store's database role can see. In the vulnerable DB-role default
(`store_owner`, VULN-DBROLE-01) that is the whole `duckstore` database — customers, password
hashes, saved-card tokens.

## Confirm it

The probe sends a **benign marker** UNION — it returns a constant row
(`sku = SQLI-PROOF`) rather than reading any sensitive table. If that row comes back, the input
changed the SQL, which proves the injection point. (The same injection *shape* can read other
tables; this catalogue does not ship a data-exfiltration payload — the proof is enough to confirm
and to fix.)

```bash
bash attacks/VULN-SQLI-01/check.sh
```

- **Vulnerable:** the response includes a product row with SKU `SQLI-PROOF` that does not exist in
  the catalogue — it came from the injected `UNION SELECT`.
- **Secure:** the marker never appears; the text is treated as a literal search term (likely zero
  results), because `q` is a bound parameter.

## Observe in telemetry

The Npgsql client span on store-api shows the **executed SQL**. In vulnerable mode the statement
text contains the injected `UNION SELECT …`; in secure mode it is the fixed parameterised query with
a bound `@q`. A spike of `products/search` requests whose db statements vary wildly (UNIONs,
comments `--`, `OR 1=1`) is the hunt signature. See
[detections/VULN-SQLI-01.md](../../detections/VULN-SQLI-01.md).

## Fix

Flip `VULN-SQLI-01` to **secure**: the secure `IProductSearch` binds `q` as a parameter, so input
can never change the statement. Pair with least-privilege DB role (VULN-DBROLE-01 secure) so an
injection that does slip through can read far less.

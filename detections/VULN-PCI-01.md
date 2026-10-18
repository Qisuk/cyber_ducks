# Detect VULN-PCI-01 — card data at rest

At rest, not in traffic. Detect by scanning stored data for PANs and any CVV.

## In the data (SQL)

```sql
SELECT count(*) AS with_pan FROM saved_cards WHERE card_number IS NOT NULL;
SELECT count(*) AS with_cvv FROM saved_cards WHERE card_cvv    IS NOT NULL;   -- must always be 0
-- PAN-shaped strings anywhere they shouldn't be (e.g. notes, logs):
SELECT id FROM orders WHERE notes ~ '\d{13,19}';
```

Any non-null `card_cvv` is an immediate PCI DSS violation; a stored full `card_number` nearly so.

## Which mode is active?

PCI-01 is a data property (not a code span). Confirm via the data above, and via whether the
range-control **purge** has been run (it clears `card_number`/`card_cvv`).

## Phase 8 / hardening

Prevent: never store CVV; tokenise the PAN (keep token + last4). A DLP/secrets scan over the DB and
logs for Luhn-valid card numbers is the standing detection.

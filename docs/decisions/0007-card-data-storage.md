# ADR-0007: Store card data in the store DB (vulnerable), with tokenisation as the hardened mode

- **Status:** Accepted
- **Date:** 2026-10-10

## Context
Customers pay invoices by card. A real store would send card details straight to a payment
provider and keep only a token. The range needs a high-value data asset (threat-model asset:
PCI data) for SQL injection, IDOR and AI-agent data-leak scenarios.

## Options
1. **Tokenised:** card data goes only to `payment-stub`; the store keeps `pm_xxx` tokens and the last 4 digits.
2. **Stored:** "Remember my card" saves the PAN, expiry and **CVV** in Postgres in plain text.

## Decision
Option 2 by default, behind toggle `Vulns__StoreCardData` (ADR-0006). With the toggle off, the
store uses Option 1.

## Consequences
- This deliberately violates PCI DSS: the CVV must never be stored after authorisation, and the PAN must be unreadable at rest.
  It's a good finding to report.
- Seed data uses **only** published test card numbers (e.g. `4111 1111 1111 1111`). No real card data, ever.
- The schema needs both a `saved_cards` table (vulnerable) and a token column (hardened).
- `payment-stub` must support both "charge raw card" and "charge token".

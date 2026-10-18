# VULN-PCI-01 — Full card data stored at rest (PAN + CVV)

| | |
|---|---|
| **Flag** | `VULN-PCI-01` (data) |
| **Where** | `saved_cards.card_number` / `card_cvv` ([02-schema.sql](../../infra/postgres/init/02-schema.sql), seed) |
| **Class** | OWASP A02:2021 / PCI DSS violation |
| **Threats** | asset A2 (card data) |

## The weakness

Vulnerable storage keeps the **full card number (PAN) and the CVV** in `saved_cards`. Storing the
CVV is **never** permitted under PCI DSS, and storing the PAN in the clear is a serious breach.
Secure storage keeps only a provider **token** and the **last 4 digits** — enough to show "Visa
…4242" and to charge via the provider, with no sensitive data at rest.

All seeded cards use the payment providers' **published test numbers** (e.g. `4242…4242`); they are
not real.

## Business impact

A database leak exposes customers' card data directly — fraud liability, PCI penalties, and
mandatory breach notification.

## Confirm it

```bash
bash attacks/VULN-PCI-01/check.sh
```

The probe counts and samples `saved_cards` rows that hold a PAN/CVV. (Reads the range's postgres via
`docker compose exec`; read-only.)

- **Vulnerable (as seeded):** full PAN + CVV present.
- **Secure:** only `payment_token` + `last4`, no PAN/CVV — reached by running the range-control
  **purge** action, which clears `card_number`/`card_cvv`.

## Observe in telemetry

At rest, not in traffic. Detect by scanning stored data for PAN patterns (a Luhn-valid 13–19 digit
string) and for any non-null CVV column. See [detections/VULN-PCI-01.md](../../detections/VULN-PCI-01.md).

## Fix

Never store the CVV; tokenise the PAN at the provider and keep only token + last4. Run the
range-control **purge** to clear the seeded card data. Scope card handling to a PCI-isolated path.

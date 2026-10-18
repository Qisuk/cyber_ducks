#!/usr/bin/env bash
# VULN-PCI-01 — confirm full card numbers / CVVs are stored at rest.
# Reads the range's postgres (forensic view). Seeded cards use provider TEST numbers only.
set -eu
cd "$(dirname "$0")/.."
. ./_dbquery.sh

echo "VULN-PCI-01 — are full card numbers and CVVs stored (never allowed under PCI DSS)?"
echo "----------------------------------------------------------------------"

rows=$(db_query duckstore "SELECT count(*) FROM saved_cards WHERE card_number IS NOT NULL OR card_cvv IS NOT NULL")
echo "saved_cards rows holding a full PAN and/or CVV: ${rows:-0}"

if [ "${rows:-0}" != "0" ]; then
    echo "VULNERABLE: full card data is stored at rest. Storing the CVV is never permitted under PCI DSS."
    echo "  Sample (seeded TEST numbers only):"
    db_query duckstore "SELECT cardholder_name, card_number, card_cvv, payment_token FROM saved_cards ORDER BY id LIMIT 3" \
        | sed 's/|/  /g; s/^/    /'
    echo "  Secure storage keeps only the provider token + last4 (payment_token, last4) and no PAN/CVV."
    echo "  The range-control 'purge' action clears card_number/card_cvv."
else
    echo "SECURE at rest: no full PAN/CVV stored — only tokens + last4."
fi

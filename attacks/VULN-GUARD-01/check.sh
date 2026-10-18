#!/usr/bin/env bash
# VULN-GUARD-01 — confirm the database guard "order_items_quantity_positive".
# Places an order with quantity -1. The app does not check quantity; only the DB guard does.
# Local range, seeded fake data; reset with: docker compose down -v
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-GUARD-01 — does the database reject an order line with a negative quantity?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

code=$(curl -s -m 10 -o /dev/null -w '%{http_code}' -X POST "$STORE/api/orders" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d '{"shippingAddress":"1 Test Way","notes":"VULN-GUARD-01 probe","items":[{"productId":1,"quantity":-1,"unitPrice":4.99}]}')
echo "Order with quantity -1 -> HTTP $code"

case "$code" in
    200) echo "GUARD OFF (vulnerable): the negative-quantity order was stored. The app never checked"
         echo "  quantity, and no DB guard caught it — bad data at rest."
         echo "  → Turn the 'order_items_quantity_positive' guard ON in Range Control (Database guards)"
         echo "    and re-run; the insert is rejected (HTTP 500)." ;;
    500) echo "GUARD ON (secure): the database rejected the negative quantity (second-layer defence)." ;;
    *)   echo "Unexpected response ($code). Is the range running and seeded?" ;;
esac

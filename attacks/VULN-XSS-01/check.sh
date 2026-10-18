#!/usr/bin/env bash
# VULN-XSS-01 — confirm stored XSS: is a review body rendered as HTML or escaped?
# Uses a BENIGN, inert marker element (no script) to prove HTML injection. Local range, seeded data;
# reset with: docker compose down -v
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

PRODUCT=1
MARKER="xssprobe$$"
# Inert proof: an italic tag wrapping a marker, no attributes/quotes (keeps the JSON clean).
# Renders as formatting if unescaped; no script executes.
BODY="great duck <i>$MARKER</i>"

echo "VULN-XSS-01 — is a review body rendered as raw HTML (stored XSS) or escaped?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

# Store the review via the API (storage is always raw; the weakness is in how store-web renders it).
curl -s -m 10 -o /dev/null -X POST "$STORE/api/products/$PRODUCT/reviews" \
    -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d "{\"rating\":5,\"body\":\"$BODY\"}"

# Render the product page in store-web and see how the body came out.
page=$(curl -s -m 10 "$STORE_WEB/ducks/$PRODUCT")

if printf '%s' "$page" | grep -q "<i>$MARKER</i>"; then
    echo "VULNERABLE: the review body was rendered as raw HTML (the <i> element is live in the page)."
    echo "  A review body of <script>… would execute in every visitor's browser (stored XSS)."
    echo "  → Flip VULN-XSS-01 to secure in Range Control and re-run; the tag is escaped."
elif printf '%s' "$page" | grep -q "&lt;i&gt;$MARKER&lt;/i&gt;"; then
    echo "SECURE: the body was HTML-encoded (shown as text, not rendered). No stored XSS."
else
    echo "Could not find the marker in the page. The review may not have rendered yet; try the"
    echo "product page http://localhost:8080/ducks/$PRODUCT by hand."
fi

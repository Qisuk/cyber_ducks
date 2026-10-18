#!/usr/bin/env bash
# VULN-CSRF-01 — confirm whether a state-changing form post is accepted WITHOUT an anti-forgery
# token (as a cross-site request would be). Uses the logged-in cookie but omits the token.
# Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

jar="$(mktemp)"
trap 'rm -f "$jar"' EXIT

echo "VULN-CSRF-01 — is a form post accepted without an anti-forgery token?"
hr

if ! web_login "$jar" alice@duckstore.test password1; then
    echo "store-web login failed (is the range seeded?)." >&2; exit 2
fi

# Post the profile-change form with the auth cookie but NO anti-forgery token — what a forged
# cross-site form submits. A real CSRF would also not carry the antiforgery cookie.
code=$(curl -s -m 10 -b "$jar" -o /dev/null -w '%{http_code}' \
    -X POST "$STORE_WEB/account/profile" --data-urlencode "displayName=CSRF probe")
echo "Form post without anti-forgery token -> HTTP $code"

case "$code" in
    200|302) echo "VULNERABLE: the state-changing post was accepted without a token. A cross-site form"
             echo "  could perform this action as the logged-in user (CSRF)."
             echo "  → Flip VULN-CSRF-01 to secure in Range Control and re-run; expect 400 (rejected)." ;;
    400)     echo "SECURE: the post was rejected (400) — a valid anti-forgery token is required." ;;
    401|404) echo "Unexpected $code — the endpoint may require a different session. Check you are logged in." ;;
    *)       echo "Unexpected response ($code). Is the range running and seeded?" ;;
esac

#!/usr/bin/env bash
# VULN-AUTH-01 — confirm login hardening: does a failed login reveal WHICH field was wrong
# (user enumeration)? This is the non-destructive signal; it does not trigger a lockout.
# The lockout/throttling half is described in the README (not exercised here, to avoid locking
# a seeded account). Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-AUTH-01 — do login failures reveal whether the account exists (user enumeration)?"
hr

# Same wrong password against a known account vs an account that does not exist.
known=$(curl -s -m 10 -X POST "$STORE/api/auth/login" -H 'Content-Type: application/json' \
    -d '{"email":"alice@duckstore.test","password":"definitely-wrong"}')
unknown=$(curl -s -m 10 -X POST "$STORE/api/auth/login" -H 'Content-Type: application/json' \
    -d '{"email":"no-such-user@duckstore.test","password":"definitely-wrong"}')

echo "known account   → $known"
echo "unknown account → $unknown"

if [ "$known" != "$unknown" ]; then
    echo "VULNERABLE: the responses differ, so an attacker can tell which emails are registered"
    echo "  (user enumeration). Vulnerable mode also has no lockout — see the README."
    echo "  → Flip VULN-AUTH-01 to secure in Range Control and re-run; the messages become identical."
else
    echo "SECURE: identical responses for known vs unknown accounts (no enumeration). Secure mode"
    echo "  also locks an account after repeated failures — see the README."
fi

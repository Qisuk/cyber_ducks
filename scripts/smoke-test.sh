#!/usr/bin/env bash
# Cyber Ducks range pre-flight smoke test. Verifies the range is up, healthy, and ready to run an
# exercise. Exit code 0 = all good, 1 = something needs attention. Safe to run any time (read-only).
set -uo pipefail

COMPOSE="${COMPOSE:-docker compose}"
STORE_PORT="${STORE_PORT:-8080}"
RANGE_CONTROL_PORT="${RANGE_CONTROL_PORT:-8090}"
GRAFANA_PORT="${GRAFANA_PORT:-3000}"
fail=0

ok()   { printf '  \033[32m✓\033[0m %s\n' "$1"; }
bad()  { printf '  \033[31m✗\033[0m %s\n' "$1"; fail=1; }
note() { printf '  \033[33m•\033[0m %s\n' "$1"; }

echo "== Containers =="
unhealthy=$($COMPOSE ps --format '{{.Name}} {{.Health}}' 2>/dev/null \
  | grep -vE 'healthy|backup|otel-collector|postgres-exporter' | grep -v '^\s*$' || true)
if [ -z "$unhealthy" ]; then ok "all containers healthy"; else bad "unhealthy: $unhealthy"; fi

echo "== Surfaces =="
code() { curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$1" 2>/dev/null; }
[ "$(code http://127.0.0.1:$STORE_PORT/)" = 200 ] && ok "store (host)" || bad "store (host) unreachable"
rc=$(code http://127.0.0.1:$RANGE_CONTROL_PORT/); [ "$rc" = 302 ] || [ "$rc" = 200 ] && ok "range control" || bad "range control unreachable ($rc)"
[ "$(code http://127.0.0.1:$GRAFANA_PORT/)" = 200 ] && ok "grafana" || bad "grafana unreachable"

echo "== Flags =="
placed=$($COMPOSE exec -T postgres psql -U duckadmin -d range_control -t -A \
  -c "SELECT count(*) FILTER (WHERE placed)||'/'||count(*) FROM ctf_flags;" 2>/dev/null | tr -d '[:space:]')
[ -n "$placed" ] && ok "CTF flags placed: $placed" || bad "could not read ctf_flags"

echo "== Defences =="
flagcnt=$($COMPOSE exec -T postgres psql -U duckadmin -d range_control -t -A \
  -c "SELECT count(*) FILTER (WHERE mode='secure')||' secure / '||count(*)||' total' FROM defence_flags;" 2>/dev/null | tr -d '\r')
[ -n "$flagcnt" ] && ok "defence flags: $flagcnt" || bad "could not read defence_flags"

echo "== LLM =="
model=$($COMPOSE exec -T ollama ollama list 2>/dev/null | awk 'NR==2{print $1}')
[ -n "$model" ] && ok "ollama model present: $model" || note "no ollama model resident yet (first chat will be slow)"

echo
[ "$fail" = 0 ] && echo "Smoke test PASSED." || echo "Smoke test found issues — see ✗ above."
exit $fail

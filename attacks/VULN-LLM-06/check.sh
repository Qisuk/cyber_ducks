#!/usr/bin/env bash
# VULN-LLM-06 — this path runs through the LLM, so it is confirmed manually in the browser.
# This script prints the steps and offers to run the deterministic, model-free xUnit check.
set -eu
cd "$(dirname "$0")/.."
. ./_lib.sh

cat <<'STEPS'
VULN-LLM-06 — agent cancels without confirmation.

Manual check (browser):
  1. Sign in to http://localhost:8080 as alice@duckstore.test / password1
  2. Open "Ask the Duck" and send: Please cancel my order 1014.
  3. Secure:     a "Confirm: cancel order #1014" button appears; order stays 'placed'.
     Vulnerable: order 1014 is cancelled during the chat, no button.

Deterministic check (no LLM): the xUnit tests assert the secure tool never calls
store-api and the vulnerable tool does.
STEPS

if command -v dotnet >/dev/null 2>&1; then
    printf '\nRun the deterministic tests now? [y/N] '
    read -r ans || ans=n
    case "$ans" in
        y|Y)
            dotnet test "$(git rev-parse --show-toplevel)/tests/CyberDucks.Tests" \
                --filter 'FullyQualifiedName~AgentDefenceTests' ;;
        *) echo "Skipped." ;;
    esac
fi

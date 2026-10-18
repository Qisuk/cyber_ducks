# Shared helpers for the range confirmation probes. Source this from a check.sh.
# POSIX sh + curl. Local range only, seeded fake data, fake credentials.

# store-api, published on localhost for direct API testing (see compose.yaml). The browser-facing
# store-web is on :8080; these API-level probes talk to store-api on :8081.
STORE="${STORE_API_URL:-http://localhost:8081}"

# Refuse to run against anything but the local range. These scripts demonstrate deliberate
# weaknesses; they must never be pointed at a real host.
guard_localhost() {
    host=$(printf '%s' "$STORE" | sed -E 's#^https?://([^/:]+).*#\1#')
    case "$host" in
        localhost|127.0.0.1|::1|0.0.0.0) ;;
        *)
            echo "Refusing to run: STORE_URL host '$host' is not local. This probe is for your own range only." >&2
            exit 2
            ;;
    esac
}

need() {
    command -v "$1" >/dev/null 2>&1 || { echo "Missing required tool: $1" >&2; exit 2; }
}

# Check the store is up before probing.
require_store() {
    if ! curl -fs -m 5 "$STORE/health/ready" >/dev/null 2>&1; then
        echo "Store not reachable at $STORE. Start the range: docker compose up -d" >&2
        exit 2
    fi
}

# Log in a seeded persona, echo the JWT. Fake credentials for the local range.
# Usage: token=$(login alice@duckstore.test password1)
login() {
    curl -fs -m 10 -X POST "$STORE/api/auth/login" \
        -H 'Content-Type: application/json' \
        -d "{\"email\":\"$1\",\"password\":\"$2\"}" \
    | sed -n 's/.*"token":"\([^"]*\)".*/\1/p'
}

# The browser-facing store-web (cookie auth), for probes that exercise the UI tier.
STORE_WEB="${STORE_WEB_URL:-http://localhost:8080}"

# Sign in to store-web with a cookie jar (form login sets the duck_auth cookie).
# Usage: web_login <jar> <email> <password>  -> 0 on success
web_login() {
    # No returnUrl field: store-web currently 500s when login is given one (known bug).
    curl -s -m 10 -c "$1" -b "$1" -o /dev/null \
        -X POST "$STORE_WEB/account/login" \
        --data-urlencode "email=$2" --data-urlencode "password=$3"
    # A set duck_auth cookie means success.
    grep -q 'duck_auth' "$1"
}

hr() { printf '%s\n' '----------------------------------------------------------------------'; }

# Helper for data-at-rest probes: run a read-only SQL query inside the range's postgres container.
# Unlike the curl probes, these inspect what is stored (a forensic/defender view), so they use
# `docker compose exec` rather than a network request. Local range only.

db_query() {
    # $1 = database, $2 = SQL
    if ! command -v docker >/dev/null 2>&1; then
        echo "Missing required tool: docker (data-at-rest probes read the range's postgres)." >&2
        exit 2
    fi
    docker compose exec -T postgres psql -U duckadmin -d "$1" -tAc "$2" 2>/dev/null
}

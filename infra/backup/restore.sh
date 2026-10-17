#!/usr/bin/env bash
# Restore duckstore from a dump in the backups volume.
# Runs INSIDE the postgres container (backups mounted read-only at /backups), so the backup
# service never needs superuser credentials.
#
#   docker compose exec postgres bash /backup-scripts/restore.sh               # latest dump
#   docker compose exec postgres bash /backup-scripts/restore.sh <file.dump>   # specific dump
#
# Verifies the SHA-256 checksum first. Note: the checksum sits next to the dump in the same
# volume, so an attacker who can rewrite the dump can rewrite the checksum too (threat model:
# tampered restores). It catches corruption, not a determined adversary.
set -euo pipefail

BACKUP_DIR=/backups
DB=duckstore

file="${1:-$(ls -1t "$BACKUP_DIR"/"$DB"-*.dump 2>/dev/null | head -n 1)}"
[[ "$file" == /* ]] || file="$BACKUP_DIR/$file"
[[ -f "$file" ]] || { echo "restore: no dump found (${file:-none})" >&2; exit 1; }

echo "restore: verifying checksum of $file"
(cd "$BACKUP_DIR" && sha256sum --check --quiet "$(basename "$file").sha256")

echo "restore: disconnecting other sessions from $DB"
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres -qtAc \
    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB' AND pid <> pg_backend_pid();" > /dev/null

echo "restore: restoring $file into $DB"
# --clean --if-exists drops each object before recreating it; one transaction, so a failure
# leaves the database unchanged.
pg_restore -U "$POSTGRES_USER" -d "$DB" --clean --if-exists --single-transaction --exit-on-error "$file"

echo "restore: done"

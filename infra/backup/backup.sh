#!/usr/bin/env bash
# Scheduled pg_dump of duckstore into the pgbackups volume (zone Z6, flow F17).
# Runs in the `backup` service as backup_reader (read-only: pg_read_all_data).
# Usage: backup.sh            loop forever (service mode)
#        backup.sh --once     take one backup and exit
# Range note: dumps are NOT encrypted, and the volume isn't append-only. Both are deliberate
# weaknesses for now (threat model: backups, A15).
set -euo pipefail

: "${BACKUP_INTERVAL_SECONDS:=3600}"
: "${BACKUP_KEEP:=24}"
BACKUP_DIR=/backups
DB=duckstore

take_backup() {
    local stamp file
    stamp=$(date -u +%Y%m%dT%H%M%SZ)
    file="$BACKUP_DIR/$DB-$stamp.dump"
    # Custom format (-Fc): compressed, and restorable selectively with pg_restore.
    pg_dump --format=custom --file="$file.partial" "$DB"
    mv "$file.partial" "$file"
    (cd "$BACKUP_DIR" && sha256sum "$(basename "$file")" > "$(basename "$file").sha256")
    echo "backup: wrote $file ($(du -h "$file" | cut -f1))"

    # Keep the newest $BACKUP_KEEP dumps; delete older ones and their checksums.
    ls -1t "$BACKUP_DIR"/"$DB"-*.dump 2>/dev/null | tail -n +"$((BACKUP_KEEP + 1))" | while read -r old; do
        rm -f "$old" "$old.sha256"
        echo "backup: pruned $old"
    done
}

if [[ "${1:-}" == "--once" ]]; then
    take_backup
    exit 0
fi

echo "backup: every ${BACKUP_INTERVAL_SECONDS}s, keeping ${BACKUP_KEEP}"
while true; do
    take_backup || echo "backup: FAILED" >&2
    sleep "$BACKUP_INTERVAL_SECONDS"
done

#!/usr/bin/env bash
set -Eeuo pipefail

cd "$(dirname "$0")"
if [[ ! -f .env ]]; then echo ".env bulunamadı." >&2; exit 1; fi

env_value() {
  sed -n "s/^$1=//p" .env | tail -n 1
}

POSTGRES_USER="${POSTGRES_USER:-$(env_value POSTGRES_USER)}"
POSTGRES_DB="${POSTGRES_DB:-$(env_value POSTGRES_DB)}"
BACKUP_DIR="${BACKUP_DIR:-$(env_value BACKUP_DIR)}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-$(env_value BACKUP_RETENTION_DAYS)}"
: "${POSTGRES_USER:?POSTGRES_USER .env içinde tanımlı değil}"
: "${POSTGRES_DB:?POSTGRES_DB .env içinde tanımlı değil}"

backup_dir="${BACKUP_DIR:-/var/backups/valem}"
retention_days="${BACKUP_RETENTION_DAYS:-14}"
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
umask 077

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
target="$backup_dir/valem-$timestamp.dump"
temporary="$target.partial"
trap 'rm -f "$temporary"' EXIT

docker compose --env-file .env -f compose.yml exec -T postgres \
  pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --format=custom --compress=9 --no-owner --no-privileges > "$temporary"

test -s "$temporary"
mv "$temporary" "$target"
sha256sum "$target" > "$target.sha256"
find "$backup_dir" -maxdepth 1 -type f \( -name 'valem-*.dump' -o -name 'valem-*.dump.sha256' \) \
  -mtime "+$retention_days" -delete

echo "$target"

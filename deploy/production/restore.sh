#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 ]]; then echo "Kullanım: $0 /tam/yol/valem-YYYYMMDD.dump" >&2; exit 1; fi
dump_file="$(realpath "$1")"
if [[ ! -s "$dump_file" ]]; then echo "Yedek dosyası bulunamadı veya boş." >&2; exit 1; fi

cd "$(dirname "$0")"
if [[ ! -f .env ]]; then echo ".env bulunamadı." >&2; exit 1; fi

env_value() {
  sed -n "s/^$1=//p" .env | tail -n 1
}

POSTGRES_USER="${POSTGRES_USER:-$(env_value POSTGRES_USER)}"
POSTGRES_DB="${POSTGRES_DB:-$(env_value POSTGRES_DB)}"
: "${POSTGRES_USER:?POSTGRES_USER .env içinde tanımlı değil}"
: "${POSTGRES_DB:?POSTGRES_DB .env içinde tanımlı değil}"

read -r -p "Mevcut VPS veritabanının üzerine dönmek için RESTORE VALEM yazın: " confirmation
if [[ "$confirmation" != "RESTORE VALEM" ]]; then echo "Geri yükleme iptal edildi." >&2; exit 1; fi

compose_files=(-f compose.yml)
edge_service=caddy
if [[ -s secrets/cloudflare-tunnel-credentials.json ]]; then
  compose_files+=(-f compose.tunnel.yml)
  edge_service=cloudflared
fi

docker compose --env-file .env "${compose_files[@]}" up -d postgres
docker compose --env-file .env "${compose_files[@]}" stop api 2>/dev/null || true
restart_api() { docker compose --env-file .env "${compose_files[@]}" up -d api "$edge_service" >/dev/null 2>&1 || true; }
trap restart_api EXIT

cat "$dump_file" | docker compose --env-file .env "${compose_files[@]}" exec -T postgres \
  pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --clean --if-exists --no-owner --no-privileges --exit-on-error

docker compose --env-file .env "${compose_files[@]}" up -d api "$edge_service"
trap - EXIT
echo "Geri yükleme tamamlandı. /health/ready adresini doğrulayın."

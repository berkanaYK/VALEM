#!/usr/bin/env bash
set -Eeuo pipefail

repo_dir="$(cd "$(dirname "$0")/../.." && pwd)"
deploy_dir="$repo_dir/deploy/production"
lock_file="${VALEM_DEPLOY_LOCK:-/tmp/valem-deploy.lock}"
exec 9>"$lock_file"
flock -n 9 || exit 0

cd "$repo_dir"
git fetch --force --tags origin main
target="$(git tag --merged origin/main --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n 1 || true)"
[[ -n "$target" ]] || { echo "Dağıtılabilir sürüm etiketi bulunamadı." >&2; exit 1; }
current="$(git describe --tags --exact-match HEAD 2>/dev/null || git rev-parse HEAD)"
[[ "$current" == "$target" ]] && exit 0

previous="$(git rev-parse HEAD)"
cd "$deploy_dir"
if docker compose --env-file .env -f compose.yml -f compose.tunnel.yml ps --status running postgres 2>/dev/null | grep -q postgres; then
  ./backup.sh
fi

cd "$repo_dir"
git checkout --detach "$target"
cd "$deploy_dir"
if docker compose --env-file .env -f compose.yml -f compose.tunnel.yml build --pull api \
  && docker compose --env-file .env -f compose.yml -f compose.tunnel.yml up -d --wait postgres api cloudflared; then
  ./setup-readonly-db.sh
  echo "VALEM $target dağıtıldı."
  exit 0
fi

echo "VALEM $target dağıtımı başarısız; $previous sürümüne dönülüyor." >&2
cd "$repo_dir"
git checkout --detach "$previous"
cd "$deploy_dir"
docker compose --env-file .env -f compose.yml -f compose.tunnel.yml build api
docker compose --env-file .env -f compose.yml -f compose.tunnel.yml up -d --wait postgres api cloudflared
./setup-readonly-db.sh
exit 1

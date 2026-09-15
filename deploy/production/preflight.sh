#!/usr/bin/env bash
set -Eeuo pipefail

cd "$(dirname "$0")"
mode="${1:-standalone}"
if [[ "$mode" != "standalone" && "$mode" != "shared-host" && "$mode" != "tunnel" ]]; then
  echo "Kullanım: $0 [standalone|shared-host|tunnel]" >&2
  exit 1
fi

for command_name in docker curl openssl sed awk; do
  command -v "$command_name" >/dev/null 2>&1 || {
    echo "Eksik komut: $command_name" >&2
    exit 1
  }
done
docker compose version >/dev/null
docker info >/dev/null

memory_kb="$(awk '/MemTotal:/ {print $2}' /proc/meminfo)"
total_kb="$(df -Pk . | awk 'NR==2 {print $2}')"
available_kb="$(df -Pk . | awk 'NR==2 {print $4}')"
(( memory_kb >= 7 * 1024 * 1024 )) || {
  echo "En az 8 GB RAM ayrılmalı; görülen bellek 7 GB altında." >&2
  exit 1
}
(( total_kb >= 45 * 1024 * 1024 && available_kb >= 20 * 1024 * 1024 )) || {
  echo "En az 45 GB disk ve kurulum öncesi 20 GB boş alan gerekli." >&2
  exit 1
}

if [[ ! -f .env ]]; then
  echo ".env bulunamadı; önce sudo ./init-env.sh çalıştırın." >&2
  exit 1
fi
if [[ ! -s secrets/dataprotection.pfx ]]; then
  echo "Data Protection sertifikası bulunamadı." >&2
  exit 1
fi

env_mode="$(stat -c '%a' .env)"
[[ "$env_mode" == "600" ]] || {
  echo ".env izni 600 olmalı; görülen: $env_mode" >&2
  exit 1
}

compose_args=(--env-file .env -f compose.yml)
if [[ "$mode" == "shared-host" ]]; then
  compose_args+=(-f compose.shared-host.yml)
elif [[ "$mode" == "tunnel" ]]; then
  compose_args+=(-f compose.tunnel.yml)
  [[ -s secrets/cloudflare-tunnel-token ]] || {
    echo "secrets/cloudflare-tunnel-token bulunamadı veya boş." >&2
    exit 1
  }
  token_mode="$(stat -c '%a' secrets/cloudflare-tunnel-token)"
  [[ "$token_mode" == "600" ]] || {
    echo "Tunnel belirteci dosya izni 600 olmalı; görülen: $token_mode" >&2
    exit 1
  }
else
  if command -v ss >/dev/null 2>&1 && ss -H -ltn '( sport = :80 or sport = :443 )' | grep -q .; then
    echo "80 veya 443 portu kullanımda. Ayrı IP yoksa shared-host modunu kullanın." >&2
    exit 1
  fi
fi

docker compose "${compose_args[@]}" config --quiet
echo "VALEM ön kontrolü başarılı: mode=$mode, memory_kb=$memory_kb, total_kb=$total_kb, available_kb=$available_kb"

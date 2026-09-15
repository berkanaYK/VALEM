#!/usr/bin/env bash
set -Eeuo pipefail

cd "$(dirname "$0")"
[[ -f .env ]] || { echo ".env bulunamadı." >&2; exit 1; }

env_value() {
  sed -n "s/^$1=//p" .env | tail -n 1
}

readonly_password="$(env_value DEVELOPER_DB_PASSWORD)"
postgres_user="$(env_value POSTGRES_USER)"
postgres_db="$(env_value POSTGRES_DB)"
[[ "$readonly_password" =~ ^[0-9a-f]{64}$ ]] || {
  echo "DEVELOPER_DB_PASSWORD güvenli 64 karakterli hex değer olmalı." >&2
  exit 1
}

docker compose --env-file .env -f compose.yml exec -T postgres \
  psql --username "$postgres_user" --dbname "$postgres_db" --set ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'valem_readonly') THEN
    CREATE ROLE valem_readonly LOGIN PASSWORD '$readonly_password'
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  ELSE
    ALTER ROLE valem_readonly PASSWORD '$readonly_password'
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
  END IF;
END
\$\$;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE "$postgres_db" TO valem_readonly;
GRANT USAGE ON SCHEMA public TO valem_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO valem_readonly;
ALTER DEFAULT PRIVILEGES FOR ROLE "$postgres_user" IN SCHEMA public GRANT SELECT ON TABLES TO valem_readonly;
SQL

echo "Salt okunur geliştirici veritabanı hesabı hazır."

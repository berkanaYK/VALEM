#!/usr/bin/env sh
set -eu

cd "$(dirname "$0")"
umask 077

if [ -f .env ]; then
    echo ".env zaten var; mevcut sırlar korunuyor." >&2
    exit 1
fi

printf "API alan adı (ör. api.valem.com): "
read -r domain
printf "Yönetim paneli alan adı (ör. panel.valem.com): "
read -r panel_domain
printf "Platform yönetici e-postası: "
read -r admin_email

case "$domain" in
    ""|*/*|*:*|*" "*) echo "Geçerli, yalnızca alan adı girin." >&2; exit 1 ;;
esac
case "$panel_domain" in
    ""|*/*|*:*|*" "*) echo "Geçerli, yalnızca panel alan adı girin." >&2; exit 1 ;;
esac
case "$admin_email" in
    *@*.*) ;;
    *) echo "Geçerli bir e-posta girin." >&2; exit 1 ;;
esac

postgres_password="$(openssl rand -hex 32)"
jwt_key="$(openssl rand -base64 64 | tr -d '\n')"
admin_password="Va1!$(openssl rand -hex 14)"
certificate_password="$(openssl rand -hex 32)"
developer_db_password="$(openssl rand -hex 32)"

mkdir -p secrets
chmod 700 secrets
openssl req -x509 -newkey rsa:3072 -sha256 -nodes \
  -subj "/CN=VALEM Data Protection" -days 3650 \
  -keyout secrets/dataprotection.key -out secrets/dataprotection.crt >/dev/null 2>&1
openssl pkcs12 -export -out secrets/dataprotection.pfx \
  -inkey secrets/dataprotection.key -in secrets/dataprotection.crt \
  -passout "pass:$certificate_password" >/dev/null 2>&1
rm -f secrets/dataprotection.key secrets/dataprotection.crt
chown 1654:1654 secrets/dataprotection.pfx
chmod 400 secrets/dataprotection.pfx

cat > .env <<EOF
VALEM_DOMAIN=$domain
VALEM_PANEL_DOMAIN=$panel_domain
VALEM_IMAGE_TAG=3.5.3
HTTP_PORT=80
HTTPS_PORT=443
API_BIND_ADDRESS=127.0.0.1
API_HOST_PORT=50180
POSTGRES_DB=valem
POSTGRES_USER=valem
POSTGRES_PASSWORD=$postgres_password
DB_MAX_POOL_SIZE=40
POSTGRES_MAX_CONNECTIONS=100
POSTGRES_SHARED_BUFFERS=1GB
POSTGRES_EFFECTIVE_CACHE_SIZE=3GB
POSTGRES_MAINTENANCE_WORK_MEM=256MB
POSTGRES_WORK_MEM=8MB
POSTGRES_SLOW_QUERY_MS=1000
POSTGRES_CPU_LIMIT=2.0
POSTGRES_MEMORY_LIMIT=4g
POSTGRES_MEMORY_RESERVATION=2g
POSTGRES_SHM_SIZE=512m
API_CPU_LIMIT=1.5
API_MEMORY_LIMIT=2g
API_MEMORY_RESERVATION=512m
CADDY_CPU_LIMIT=0.5
CADDY_MEMORY_LIMIT=512m
CADDY_MEMORY_RESERVATION=128m
JWT_KEY=$jwt_key
DATA_PROTECTION_CERTIFICATE_PASSWORD=$certificate_password
JWT_ISSUER=VALE.Api
JWT_AUDIENCE=VALE.Client
JWT_EXPIRY_MINUTES=480
DEVICE_SESSION_LIFETIME_DAYS=30
PLATFORM_ADMIN_EMAIL=$admin_email
PLATFORM_ADMIN_PASSWORD=$admin_password
PLATFORM_ADMIN_FULL_NAME=VALEM Platform Yöneticisi
EMAIL_ENABLED=false
EMAIL_TRANSPORT=Brevo
EMAIL_BREVO_API_KEY=
EMAIL_BREVO_BASE_URL=https://api.brevo.com/v3
EMAIL_FROM_EMAIL=
EMAIL_FROM_NAME=VALEM
FIREBASE_ENABLED=false
FIREBASE_PROJECT_ID=
FIREBASE_SERVICE_ACCOUNT_JSON=
BILLING_GOOGLE_PLAY_ENABLED=false
BILLING_PACKAGE_NAME=com.berkanayk.vale
BILLING_PRODUCT_ID=valem_premium_lifetime
BILLING_FALLBACK_PRICE=USD 5.99
BILLING_DEMO_VEHICLE_LIMIT=50
BILLING_SERVICE_ACCOUNT_JSON=
BILLING_TEST_PREMIUM_EMAIL_0=memeloialimon@gmail.com
DEVELOPER_DB_PASSWORD=$developer_db_password
BACKUP_DIR=/var/backups/valem
BACKUP_RETENTION_DAYS=7
EOF

chmod 600 .env
touch secrets/cloudflare-tunnel-credentials.json
chmod 644 secrets/cloudflare-tunnel-credentials.json
if [ -n "${SUDO_USER:-}" ]; then
    chown "$SUDO_USER":"$SUDO_USER" .env secrets secrets/cloudflare-tunnel-credentials.json
    install -d -o "$SUDO_USER" -g "$SUDO_USER" -m 700 /var/backups/valem
else
    install -d -m 700 /var/backups/valem
fi
echo ".env oluşturuldu ve yalnızca dosya sahibi okuyabilir."
echo "İlk kurulum geçici platform parolası: $admin_password"
echo "Bu parolayı güvenli parola yöneticisine kaydedin; gerektiğinde .env üzerinden döndürün."

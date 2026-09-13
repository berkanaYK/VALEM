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
printf "Platform yönetici e-postası: "
read -r admin_email

case "$domain" in
    ""|*/*|*:*|*" "*) echo "Geçerli, yalnızca alan adı girin." >&2; exit 1 ;;
esac
case "$admin_email" in
    *@*.*) ;;
    *) echo "Geçerli bir e-posta girin." >&2; exit 1 ;;
esac

postgres_password="$(openssl rand -hex 32)"
jwt_key="$(openssl rand -base64 64 | tr -d '\n')"
admin_password="Va1!$(openssl rand -hex 14)"
certificate_password="$(openssl rand -hex 32)"

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
VALEM_IMAGE_TAG=3.4.0
HTTP_PORT=80
HTTPS_PORT=443
POSTGRES_DB=valem
POSTGRES_USER=valem
POSTGRES_PASSWORD=$postgres_password
DB_MAX_POOL_SIZE=100
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
BACKUP_DIR=/var/backups/valem
BACKUP_RETENTION_DAYS=14
EOF

chmod 600 .env
echo ".env oluşturuldu ve yalnızca dosya sahibi okuyabilir."
echo "İlk kurulum geçici platform parolası: $admin_password"
echo "Bu parolayı güvenli parola yöneticisine kaydedin ve ilk girişte değiştirin."

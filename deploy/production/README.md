# VALEM bağımsız VPS kurulumu

Bu paket, VALEM API ve PostgreSQL'i kişisel bilgisayardan bağımsız bir Linux VPS'te çalıştırır. PostgreSQL dış ağa port yayınlamaz. İnternete yalnızca Caddy üzerinden HTTP/HTTPS açılır; Caddy alan adı doğru DNS kaydına yöneldiğinde TLS sertifikasını otomatik yönetir.

## Önerilen başlangıç kapasitesi

- Ubuntu 24.04 LTS veya güncel desteklenen Debian
- VALEM için ayrılmış 4 vCPU, 8 GB RAM ve en az 50 GB SSD
- Sabit genel IPv4/IPv6 ve alan adı
- Ayrı bir sağlayıcıya günlük şifreli yedek

Bu kapasite sınırsız değildir. CPU, bellek, disk, veritabanı bağlantısı ve ağ kullanımı izlenerek VPS dikey büyütülür. Yük tek sunucunun kapasitesini aştığında API kopyaları ve yönetilen/yüksek erişilebilir PostgreSQL'e geçilir.

## İlk kurulum

1. DNS'te API alan adının `A`/`AAAA` kaydını VPS adresine yönlendirin.
2. Sunucuda güncel Docker Engine ile Compose eklentisini resmi Docker deposundan kurun.
3. Depoyu `/opt/valem` altına alın ve yalnızca `22/tcp` (sınırlandırılmış yönetim), `80/tcp`, `443/tcp` ve `443/udp` portlarını açın. `5432` portunu açmayın.
4. Ortamı ve sırları sunucuda oluşturun:

   ```bash
   cd /opt/valem/deploy/production
   chmod +x init-env.sh preflight.sh backup.sh restore.sh
   sudo ./init-env.sh
   ```

5. E-posta ve Firebase kullanılacaksa `.env` dosyasındaki ilgili değerleri doldurun. Dosya izinlerini `0600` olarak koruyun.
6. Ayrı IP'li VM/VPS ön kontrolünü çalıştırın, ardından imajı oluşturup servisleri başlatın:

   ```bash
   ./preflight.sh standalone
   docker compose --env-file .env -f compose.yml build --pull api
   docker compose --env-file .env -f compose.yml up -d
   docker compose --env-file .env -f compose.yml ps
   curl --fail --silent --show-error "https://$(sed -n 's/^VALEM_DOMAIN=//p' .env)/health/ready"
   ```

API her başlangıçta advisory lock altında EF Core migration'larını uygular. Aynı veritabanında farklı uygulama sürümlerini eş zamanlı başlatmayın.

Varsayılan kaynak dağılımı PostgreSQL için 2 CPU/4 GB, API için 1,5 CPU/2 GB ve Caddy için 0,5 CPU/512 MB'dır. Böylece 4 CPU/8 GB kotasında işletim sistemi ve kısa süreli işler için bellek bırakılır. Bu değerler `.env` üzerinden değiştirilebilir.

## Proxmox VM ve merkezi reverse proxy

VALEM ayrı bir Proxmox VM'de çalışırken arkadaşınızın merkezi reverse proxy'si 80/443 ve TLS sertifikalarını yönetebilir. `.env` içinde `API_BIND_ADDRESS` değerini VM'nin yalnızca özel ağda erişilen IP'si, `API_HOST_PORT` değerini `50180` yapın. Reverse proxy kaynak IP'si dışındaki erişimleri VM güvenlik duvarında engelleyin. PostgreSQL için host portu açmayın.

```bash
./preflight.sh shared-host
docker compose --env-file .env -f compose.yml -f compose.shared-host.yml build api
docker compose --env-file .env -f compose.yml -f compose.shared-host.yml up -d postgres api
```

Bu kip paketteki Caddy'yi başlatmaz. `api.valemyonetim.com` ve `panel.valemyonetim.com` aynı API sürecinde sunulduğu için merkezi reverse proxy her iki alan adını da `VALEM_VM_PRIVATE_IP:50180` hedefine yollar. Panel kökü `/platform-admin` yoludur. Arkadaşınız [Caddy.shared-host.example](Caddy.shared-host.example) örneğini kullandığı reverse proxy veya Kubernetes Ingress yapısına uyarlayabilir.

Cloudflare DNS'te iki `A` kaydı merkezi reverse proxy'nin genel IP adresine yöneltilir:

| Tür | Ad | Hedef |
| --- | --- | --- |
| `A` | `api` | Merkezi reverse proxy genel IPv4 adresi |
| `A` | `panel` | Merkezi reverse proxy genel IPv4 adresi |

Geçişten önce `https://api.valemyonetim.com/health/ready` ve `https://panel.valemyonetim.com/platform-admin` dış ağdan doğrulanır. Bundan sonra mobil uygulamanın `ProductionBaseUrl` değeri yeni API adresine geçirilir; DNS kurulmadan bu değeri değiştirmek mevcut sürümlerin bağlantısını keser.

## Neon verisini taşıma

Taşıma bakım penceresinde yapılır. Önce Neon'dan PostgreSQL 17 uyumlu özel biçimli yedek alın:

```bash
umask 077
pg_dump "$NEON_DATABASE_URL" --format=custom --compress=9 --no-owner --no-privileges --file=/root/valem-neon.dump
```

Ardından VPS'teki API'yi durdurup dosyayı geri yükleyin:

```bash
cd /opt/valem/deploy/production
./restore.sh /root/valem-neon.dump
```

Firma hesabı, platform hesabı, tenant sınırı, kayıt sayıları ve `/health/ready` doğrulandıktan sonra mobil uygulamanın `ProductionBaseUrl` değeri yeni HTTPS alan adına geçirilir. Bir geçiş süresi boyunca Neon/Render salt geri dönüş noktası olarak korunur.

## Güncelleme ve geri dönüş

```bash
cd /opt/valem
git pull --ff-only
cd deploy/production
./backup.sh
docker compose --env-file .env -f compose.yml build --pull api
docker compose --env-file .env -f compose.yml up -d --wait
```

Uygulama health check'i başarısızsa önceki Git commit'ine dönüp API imajını yeniden oluşturun. Şema geriye uyumlu değilse uygulama geri dönüşünden önce uygun veritabanı yedeğini izole ortamda doğrulayın.

## Otomatik yedek

`backup.sh` tutarlı `pg_dump` alır, SHA-256 dosyası üretir ve varsayılan olarak 14 günlük yerel kopya tutar. Systemd zamanlayıcısını kurmak için:

```bash
sudo cp systemd/valem-backup.* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now valem-backup.timer
systemctl list-timers valem-backup.timer
```

Yerel yedek aynı VPS arızasına karşı korumaz. `/var/backups/valem` dizinini şifreli biçimde ayrı bir S3 uyumlu depoya veya ikinci sunucuya kopyalayın ve geri yüklemeyi periyodik olarak izole bir veritabanında deneyin.

## İşletim kontrolleri

```bash
docker compose --env-file .env -f compose.yml ps
docker compose --env-file .env -f compose.yml logs --since=15m api caddy postgres
curl --fail --silent --show-error "https://$(sed -n 's/^VALEM_DOMAIN=//p' .env)/health/ready"
df -h
free -h
```

PostgreSQL yalnızca `database` adlı iç Docker ağına bağlıdır. API salt okunur kök dosya sistemi, düşürülmüş Linux capability'leri, non-root kullanıcı, sertifikayla şifrelenmiş kalıcı Data Protection anahtarları ve döndürülen Docker loglarıyla çalışır. `.env` ve `secrets/dataprotection.pfx` dosyalarını ayrı, şifreli bir yönetici kasasında yedekleyin; bunlar olmadan web oturum anahtarları başka sunucuda açılamaz.

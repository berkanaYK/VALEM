# VALEM 3.5.3 — Android build 22

Bu sürüm mobil uygulamayı kalıcı `https://api.valemyonetim.com` adresine taşır. API ve yönetim paneli Proxmox VDS üzerinde PostgreSQL 18 ile çalışır; dış erişim, sunucuda 80/443 açmadan kart gerektirmeyen yerel yönetimli Cloudflare Tunnel üzerinden sağlanır.

## Doğrulananlar

- Neon verisi VDS PostgreSQL'e aktarıldı ve yedekten geri yükleme denendi.
- Firma hesabı girişi ve yetkili dashboard isteği çalıştı.
- API hazır olma ve yetenek uçları dış HTTPS üzerinden yanıt verdi.
- Salt okunur geliştirici rolü hassas kimlik ve cihaz oturumu tablolarından ayrıldı.
- Otomatik günlük yedek ve güvenlik güncellemesi servisleri etkinleştirildi.
- Android APK/AAB kalıcı yayın anahtarıyla imzalanır; paket ve sürüm bilgileri yayın öncesi doğrulanır.

Google Play gerçek satın alma doğrulaması, Play Console servis hesabı ve ürün etkinleştirildiğinde açılacaktır.

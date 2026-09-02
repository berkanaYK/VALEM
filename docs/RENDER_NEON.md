# VALE - Render Free + Neon Free

Bu yapı VALE API'yi Render Free web service üzerinde, kalıcı PostgreSQL verisini ise Neon Free üzerinde çalıştırır.

## Neon

VALE için Neon tarafında `VALE Production` projesi kullanılır. Neon bağlantı dizesi kaynak koda yazılmaz.

## Render

Repo kökündeki `render.yaml` bir Render Blueprint'tir. Render servisinde aşağıdaki gizli değerler girilmelidir:

- `ConnectionStrings__ValeDatabase`: Neon pooled PostgreSQL bağlantı dizesi
- `Seed__AdminEmail`: İlk yönetici e-posta adresi
- `Seed__AdminPassword`: En az 10 karakter; büyük/küçük harf, rakam ve özel karakter içeren ilk yönetici parolası
- `PlatformAdmin__Email`: Firma hesaplarından ayrı geliştirici paneli e-postası
- `PlatformAdmin__Password`: Geliştirici paneli için güçlü ilk parola
- `Email__*`: Brevo API veya SMTP ayarları
- `Firebase__ProjectId` ve `Firebase__ServiceAccountJson`: FCM ayarları

Diğer JWT/servis ayarları Blueprint tarafından sağlanır. `Jwt__Key` Render tarafından rastgele oluşturulur.

API sağlık kontrolü `/health/ready` yolundadır. E-posta taşıyıcısı ayrıca `/health/email` ile doğrulanır.

Render Free web service 15 dakika istek almazsa uyuyabilir. İlk sonraki istek servisi tekrar başlatır. Veriler Render diskinde değil Neon PostgreSQL'de tutulduğu için servis uyusa veya yeniden deploy edilse bile veriler kalıcıdır.

## Android

GitHub Actions `VALE.apk` dosyasını üretir. `main` dalındaki başarılı Android build'i ayrıca GitHub Release oluşturur.

Mobil uygulama canlı Render API URL’sini varsayılan olarak kullanır; adres kullanıcıdan zorunlu olarak istenmez. Parola ve kısa ömürlü JWT kalıcı depolamaya yazılmaz. `Bu güvenli cihazda oturumu açık tut` seçilirse yalnızca dönen cihaz yenileme anahtarı Android `SecureStorage` içinde tutulur.

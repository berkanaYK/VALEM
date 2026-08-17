# VALE - Render Free + Neon Free

Bu yapı VALE API'yi Render Free web service üzerinde, kalıcı PostgreSQL verisini ise Neon Free üzerinde çalıştırır.

## Neon

VALE için Neon tarafında `VALE Production` projesi kullanılır. Neon bağlantı dizesi kaynak koda yazılmaz.

## Render

Repo kökündeki `render.yaml` bir Render Blueprint'tir. Render servisinde aşağıdaki gizli değerler girilmelidir:

- `ConnectionStrings__ValeDatabase`: Neon pooled PostgreSQL bağlantı dizesi
- `Seed__AdminEmail`: İlk yönetici e-posta adresi
- `Seed__AdminPassword`: En az 10 karakter; büyük/küçük harf, rakam ve özel karakter içeren ilk yönetici parolası

Diğer JWT/servis ayarları Blueprint tarafından sağlanır. `Jwt__Key` Render tarafından rastgele oluşturulur.

API sağlık kontrolü `/health` yolundadır.

Render Free web service 15 dakika istek almazsa uyuyabilir. İlk sonraki istek servisi tekrar başlatır. Veriler Render diskinde değil Neon PostgreSQL'de tutulduğu için servis uyusa veya yeniden deploy edilse bile veriler kalıcıdır.

## Android

GitHub Actions `VALE.apk` dosyasını üretir. `main` dalındaki başarılı Android build'i ayrıca GitHub Release oluşturur.

Mobil uygulama ilk giriş ekranında canlı Render API URL'sini bir kez ister ve cihazda saklar. Kullanıcı parolası ve JWT token kalıcı depolamaya yazılmaz.

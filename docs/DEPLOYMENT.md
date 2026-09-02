# API'yi İnternete Yayınlama

## Gerekenler

- HTTPS alan adı sunabilen bir container barındırma hizmeti
- İnternetten erişilebilen PostgreSQL/Supabase veritabanı
- Veritabanının API sunucusundan bağlantıya izin vermesi

## Container

Proje kökünde:

```powershell
docker build -t vale-api .
```

Yerelde örnek çalıştırma:

```powershell
docker run --rm -p 8080:8080 --env-file .env vale-api
```

`.env` dosyasını kaynak kontrolüne eklemeyin. `.env.example` yalnızca değişken isimlerini gösterir.

## Zorunlu ortam değişkenleri

```text
ConnectionStrings__ValeDatabase
Jwt__Key
Jwt__Issuer
Jwt__Audience
```

İlk başlangıç için ayrıca:

```text
Seed__AdminEmail
Seed__AdminPassword
Seed__AdminFullName
Seed__DefaultBranchCode
Seed__DefaultBranchName
Seed__DefaultBranchCity
```

Geliştirici web paneli için ayrıca:

```text
PlatformAdmin__Email
PlatformAdmin__Password
PlatformAdmin__FullName
```

İlk yönetici oluşturulduktan sonra `Seed__AdminPassword` değerini barındırma ortamından kaldırın. Var olan yönetici silinmediği sürece API yeniden yönetici oluşturmaya çalışmaz.

## Sağlık kontrolü

Barındırma hizmetinde sağlık yolu olarak `/health/ready`, container portu olarak `10000` kullanın.

## HTTPS ve proxy

TLS, container önündeki güvenilir ters proxy/load balancer tarafından sonlandırılabilir. Üretimde dış URL mutlaka `https://` olmalıdır. Proxy kullanıyorsanız yönlendirilmiş başlıkları yalnızca güvenilir proxy adreslerinden kabul edecek şekilde platform yapılandırmasını yapın.

## Veritabanı şeması

VALEM 3.2 sürümlü EF Core migration kullanır. API başlangıçta PostgreSQL advisory lock alır, doğrulanmış eski 3.1.2 şemasını güvenli biçimde başlangıç migration’ına bağlar ve bekleyen migration’ları uygular. Eski şemada tablo/kolon eksikse veri kaybını önlemek için deploy durur. Production kodunda `EnsureCreated` veya elle `ALTER TABLE` kullanılmaz.

## Android istemci

Android uygulaması varsayılan canlı API adresini kullanır. Özel HTTPS sunucusu gerekiyorsa uygulamadaki `Bağlantı ayarları` ekranından seçilir. APK üretme ve fiziksel cihaz testi komutları ana README’de belgelenmiştir.

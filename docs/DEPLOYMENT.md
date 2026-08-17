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

İlk yönetici oluşturulduktan sonra `Seed__AdminPassword` değerini barındırma ortamından kaldırın. Var olan yönetici silinmediği sürece API yeniden yönetici oluşturmaya çalışmaz.

## Sağlık kontrolü

Barındırma hizmetinde sağlık yolu olarak `/health`, port olarak `8080` kullanın.

## HTTPS ve proxy

TLS, container önündeki güvenilir ters proxy/load balancer tarafından sonlandırılabilir. Üretimde dış URL mutlaka `https://` olmalıdır. Proxy kullanıyorsanız yönlendirilmiş başlıkları yalnızca güvenilir proxy adreslerinden kabul edecek şekilde platform yapılandırmasını yapın.

## Veritabanı şeması

MVP ilk çalıştırmada EF Core `EnsureCreated` ile şemayı oluşturur. Üretim sonrası şema değişikliklerinde sürümlü EF Core migration modeline geçin; `EnsureCreated` ile migration aynı veritabanında karıştırılmamalıdır.

## İstemci

API yayınlandıktan sonra her şube paketinde:

```powershell
.\scripts\configure-client.ps1 -ApiBaseUrl "https://api.ornekalanadi.com/"
```

Ardından Release derlemesi:

```powershell
dotnet publish .\src\VALE.Client\VALE.Client.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true
```

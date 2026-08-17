# VALE

VALE; farklı konumlardaki şubelerin aynı sistem üzerinden araç kabulü, park durumu, teslim isteği, tahsilat ve işlem geçmişini yönetmesi için hazırlanmış bir WinUI 3 masaüstü uygulaması ve merkezi ASP.NET Core API'sidir.

## İlk sürümde bulunanlar

- E-posta/parola ile güvenli kullanıcı girişi
- Şube bazlı veri yetkilendirmesi
- Yönetici için yeni şube ve personel hesabı oluşturma
- Yönetici için aktif şube seçerek şubeler arası geçiş
- Araç, müşteri, anahtar etiketi ve park yeri kaydı
- `Teslim alındı → Park edildi → Araç isteniyor → Teslim edildi` iş akışı
- Saatlik ücret üzerinden otomatik ücret hesaplama
- Nakit, kart ve havale/EFT tahsilat kaydı
- Günlük aktif araç, teslim bekleyen araç, teslim sayısı ve ciro özeti
- Plaka, fiş numarası veya telefonla arama
- Açık, koyu ve sistem teması
- PostgreSQL/Supabase uyumlu ortak bulut veritabanı

## Mimari

```text
Şube bilgisayarları (VALE WinUI 3)
                │ HTTPS + JWT
                ▼
       VALE ASP.NET Core API
                │ TLS
                ▼
        Bulut PostgreSQL veritabanı
```

İstemci uygulama PostgreSQL'e doğrudan bağlanmaz. Veritabanı parolası yalnızca bulutta çalışan API'de tutulur.

## Teknoloji seçimi

- İstemci: C#, .NET 10, WinUI 3, Windows App SDK 2.4, CommunityToolkit.Mvvm
- API: ASP.NET Core 10 controller API, JWT, ASP.NET Core Identity
- Veri: EF Core 10 + Npgsql + PostgreSQL
- Dağıtım: API için Linux container; istemci için ilk aşamada `unpackaged` x64 Windows uygulaması

İstemci bilinçli olarak `unpackaged` seçildi: şubelerde doğrudan `.exe`/kurulum paketiyle dağıtmak ve geliştirme sırasında tekrarlanabilir CLI çalıştırma döngüsü sağlamak daha kolaydır. İstenirse daha sonra MSIX/Store paketine dönüştürülebilir.

## 1. Windows geliştirme ortamını hazırlama

Windows 11 önerilir. PowerShell'i yönetici olarak açın, proje klasörüne geçin ve çalıştırın:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\setup-windows.ps1
```

Betik; Geliştirici Modu, Visual Studio Community 2026, .NET/WinUI bileşenleri ve resmi WinUI şablonunu denetler; ardından API, testler ve WinUI istemcisini derler.

## 2. Bulut PostgreSQL hazırlama

Supabase veya yönetilen başka bir PostgreSQL hizmeti kullanılabilir. Hizmetten alınan bağlantı dizesinde TLS etkin olmalıdır. Örnek biçim:

```text
Host=SUNUCU;Port=5432;Database=postgres;Username=KULLANICI;Password=PAROLA;SSL Mode=VerifyFull
```

Bu bağlantı dizesini WinUI istemcisine kesinlikle koymayın.

## 3. Yerel geliştirme API'sini yapılandırma

```powershell
.\scripts\configure-api.ps1
.\scripts\run-api.ps1
```

İlk komut bağlantı dizesini, rastgele üretilen JWT anahtarını ve ilk yönetici hesabını .NET User Secrets içinde tutar. API ilk başlangıçta tabloları, rolleri, ilk şubeyi ve yönetici hesabını oluşturur.

Geliştirme API'si varsayılan olarak `https://localhost:7247/` adresindedir.

## 4. İnternet üzerinden erişim için API'yi yayınlama

Ortak veritabanı tek başına yeterli değildir; API'nin de internette erişilebilen bir container hizmetinde çalışması gerekir. Kök dizindeki `Dockerfile` doğrudan kullanılabilir.

Bulut ortamına şu sırları ortam değişkeni olarak ekleyin:

- `ConnectionStrings__ValeDatabase`
- `Jwt__Key` (en az 32 bayt rastgele değer)
- `Jwt__Issuer=VALE.Api`
- `Jwt__Audience=VALE.Client`
- İlk kurulumda `Seed__AdminEmail` ve `Seed__AdminPassword`

API yalnızca HTTPS üzerinden yayınlanmalıdır. Sağlık kontrolü: `GET /health`.

Ayrıntılar: [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)

## 5. Şube istemcisini API'ye bağlama

Buluttaki HTTPS API adresini ayarlayın:

```powershell
.\scripts\configure-client.ps1 -ApiBaseUrl "https://api.ornekalanadi.com/"
.\scripts\run-client.ps1
```

## 6. Son doğrulama

```powershell
.\scripts\verify.ps1
```

Betik API'yi derler, ücret hesaplama testlerini çalıştırır ve WinUI istemcisini x64 Release olarak derler. Sonrasında gerçek pencere açılışını doğrulamak için:

```powershell
.\scripts\run-client.ps1
```

## Güvenlik notları

- Veritabanı bağlantı dizesi, JWT anahtarı ve yönetici parolası kaynak kodda bulunmaz.
- Personel yalnızca hesabına atanmış şubeyi görebilir; `Admin` uygulamadaki şube seçicisinden tüm şubeler arasında geçebilir.
- Giriş endpoint'i hız sınırlıdır.
- Token uygulama belleğinde tutulur; uygulama kapatılınca silinir.
- Üretimde API HTTPS arkasında çalıştırılmalıdır.
- İlk yönetici oluşturulduktan sonra üretim ortamındaki `Seed__AdminPassword` değişkeni kaldırılmalıdır.

## Proje dizini

- `src/VALE.Client`: WinUI 3 masaüstü uygulaması
- `src/VALE.Api`: Merkezi HTTPS API
- `src/VALE.Contracts`: İstemci/API ortak veri sözleşmeleri
- `tests/VALE.Api.Tests`: İş kuralı testleri
- `scripts`: Windows kurulum, yapılandırma, çalıştırma ve doğrulama betikleri
- `docs`: Mimari, API ve yayınlama notları

## MVP sonrası önerilen geliştirmeler

- Kullanıcı devre dışı bırakma, parola sıfırlama ve ayrıntılı yetki politikaları
- QR kodlu müşteri teslim fişi
- SMS/WhatsApp araç hazır bildirimi
- İndirim, sabit tarife ve kayıp bilet işlemleri
- Denetim kaydı (audit log) ve ayrıntılı raporlama
- İnternet kesintisi için kontrollü çevrimdışı kuyruk/senkronizasyon
- İmzalı MSIX kurulum paketi ve otomatik güncelleme

# VALE Android

`src/VALE.Mobile`, mevcut VALE API ve `VALE.Contracts` projesini kullanan .NET MAUI Android istemcisidir.

## Özellikler

- Üretim API adresi uygulamada varsayılan ve giriş ekranında gizlidir
- Gelişmiş bağlantı ayarlarından özel API adresi kullanabilme
- E-posta/parola ile JWT girişi
- Parolasız e-posta kodu ve isteğe bağlı Authenticator girişi
- Güvenli cihazı hatırlama ve dönen refresh token oturumu
- Firma sahibi/personel için sade kayıt; davet koduyla hızlı katılım
- Render cold-start ve geçici ağ hataları için otomatik tek tekrar denemesi
- Günlük dashboard: aktif, istenen, teslim ve ciro kartları
- Aktif araç listesi, arama ve durum etiketleri
- Yeni araç kabulü
- `Teslim alındı -> Park edildi -> Araç isteniyor` durum akışı
- Nakit, kart veya havale/EFT ile teslim/tahsilat

Parola ve kısa ömürlü JWT erişim anahtarı kalıcı depolamaya yazılmaz. Kullanıcı isterse yalnızca dönen cihaz yenileme anahtarı Android `SecureStorage` içinde tutulur; sunucuda ham değer değil SHA-256 özeti saklanır.

Fiziksel cihaz Appium testi için ana README içindeki **Fiziksel Android cihaz testi** bölümünü ve `scripts/run-android-device-tests.ps1` betiğini kullanın.

## APK oluşturma

GitHub Actions içindeki `Build Android APK` workflow'u `main` dalına mobil dosyalarda değişiklik geldiğinde otomatik çalışır. Başarılı `main` derlemesi ayrıca GitHub Releases altında `VALE.apk` dosyasını yayınlar.

Yerelde derlemek için .NET 10 ve MAUI Android workload gerekir:

```powershell
dotnet workload install maui-android
dotnet publish .\src\VALE.Mobile\VALE.Mobile.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk
```

## API bağlantısı

Varsayılan üretim API'si:

```text
https://api.valemyonetim.com/
```

Bu adres normal giriş ekranında gösterilmez. Yalnızca özel bir sunucu kullanılacaksa `Bağlantı ayarları` üzerinden değiştirilebilir. Telefon ve Windows istemcileri aynı API adresine bağlandığında ortak PostgreSQL verisini görür.

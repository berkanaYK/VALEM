# VALEM inceleme ve düzeltme notu — 8 Eylül 2026

## Nerede kalındı?

İnceleme başlangıcı: `main`, `1b16a2c`. Son işlevsel değişiklik 4 Eylül tarihli
`a4e78fc`: parolasız e-posta OTP girişi ve bağımsız personel/deneme kaydı.
Kaynakta sürüm 3.3.1, Android build 16. Bu sürüm bilgisi yayın kanıtı değildir:
inceleme sırasında bu repo için GitHub API açık issue/PR, Actions çalıştırması
ve Release kaydı döndürmedi. Canlı ortamın hangi commit'i çalıştırdığı doğrulanmadı.

Aktif ürün MAUI Android + ASP.NET Core 10 API + PostgreSQL/Neon ve ayrı cookie
ile korunan Razor platform yönetim panelidir. WinUI istemcisi geçmişten korunuyor.
Mobil başlangıç `App → MainPage`; giriş sonrası kullanılan kabuk
`ValeAppShellV31`. Eski kabuk ve sayfaların varlığı aktif akış oldukları anlamına gelmez.

Firma sahibi kaydı Owner rolüyle aktif firma/Merkez şubesi oluşturur. Firma adı
verilen personel deneme kaydı ayrı tenant oluşturur, kullanıcıya Valet rolü verir;
bu hesap firma yönetim yetkisine sahip değildir. Mevcut firmaya davet/kodla katılım
ayrı bir akıştır ve e-posta doğrulaması ile yönetici onayı gerektirir.
Giriş yolları e-posta kodu, parola ve önceden etkinleştirilmiş Authenticator'dır.
Hatırlanan cihaz oturumları sunucuda hash olarak saklanır ve yenilemede döndürülür.

Operasyon omurgası araç kabulü, park, teslim isteği, ödeme/teslim, raporlar,
bildirimler ve denetim kayıtlarıdır. Tenant sınırı Company; şube erişimi rol ve
üyeliklerle çözülür. Şema EF Core migration ile sürümlenir.

## Bu düzeltmede

- `TenantRegistrationController.RegisterStaff` içindeki aynı adlı `sent`
  değişkenleri CS0136 oluşturuyor ve API'yi derlenemez hale getiriyordu. İç
  kapsamdaki isim değiştirildi; kayıt davranışı korunuyor.
- Son eklenen kayıt testlerinde `using Xunit` eksikti. Ayrıca test ortamında
  anonim HTTP isteği kurulmadığı için audit yazımı hata veriyordu. Test ortamı
  gerçek anonim isteği temsil edecek şekilde tamamlandı.
- E-posta koduyla girişte hatalı OTP/TOTP denemeleri Identity sayacına
  yazılmıyordu; başarılı giriş de eski hataları sıfırlamıyordu. İki hata yolu
  sayaca eklendi ve başarılı doğrulamada sayaç sıfırlandı. Beş hatadan sonra
  doğru kodla giriş de hesap kilidi nedeniyle reddediliyor.
- Test projesinin geçişli SQLitePCLRaw 2.1.11 bağımlılığı 3.0.2 bundle ile
  değiştirildi; çözümlenen native SQLite paketi SourceGear.sqlite3 3.50.4.2.
  Bu SQLite bağımlılığı testlere aittir; production API PostgreSQL kullanır.
  Kaynaklar: [güvenlik bildirimi](https://github.com/advisories/GHSA-2m69-gcr7-jv3q),
  [üreticinin bundle açıklaması](https://github.com/ericsink/SQLitePCL.raw#sourcegearsqlite3).
- `verify.ps1`, yalnız API restore edildikten sonra testleri `--no-restore`
  ile başlatıyordu; temiz klonda test assets dosyası bulunmayabilirdi. Test
  restore'u etkinleştirildi, test derleme uyarıları hata yapıldı ve sabit test
  sayısı içeren yanıltıcı başarı mesajı kaldırıldı.

## Doğrulama

| Kontrol | Sonuç |
| --- | --- |
| API Release build, `-warnaserror` | Başarılı; 0 uyarı, 0 hata |
| API testleri, Release, `-warnaserror` | 69/69 başarılı, atlanan yok |
| Yeni giriş regresyonları | Önce 3/3 başarısız, düzeltmeden sonra 3/3 başarılı |
| Appium test paketi Release build | Başarılı; 0 uyarı, 0 hata |
| EF Core pending-model-changes | Model/migration farkı yok |
| PostgreSQL idempotent migration SQL üretimi | Başarılı; veritabanına uygulanmadı |
| API test projesi dahil geçişli NuGet vulnerability audit | Bilinen açık raporlanmadı |
| Android Release build | NETSDK1147: yerel `maui-android` workload eksik |
| Gerçek Android cihaz testi | Çalıştırılmadı; `adb devices` listesi boş |

API testleri SQLite/InMemory kullanır; gerçek PostgreSQL bağlantısı, SMTP/Brevo
teslimatı, FCM ve canlı Render davranışı bu sonuçlarla doğrulanmış sayılmaz.
`verify.ps1` bütün olarak Android aşamasını tamamlamış değildir.

## Sonraki çalışma için

Önce Android workload bulunan ortamda Release/APK derlemesi ve bağlı cihazda
hazır Appium senaryosu çalıştırılmalı. Ardından test ortamında gerçek e-posta
girişi, davetle katılım, araç kabulünden tahsilata kadar günlük akış doğrulanmalı.
Play upload keystore işi README'de sonraya bırakılmış durumda; mevcut secret
değerleri veya canlı imza durumu bu incelemede okunmadı.

Değişiklikler yerelde `agent/fix-registration-auth-validation` dalındadır;
GitHub'a push, Release veya production deploy yapılmadı. Bu not kapsamlı
penetrasyon testi veya tüm ekranların sorunsuz çalıştığı iddiası değildir.

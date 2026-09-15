# Geliştirici araçları ve 3.4.0 dağıtımı

## Giriş ve yetki

Mobil: **Ayarlar / Daha → Geliştirici → VALEM yazısına çift dokunma**.
Web: `/platform-admin/database`. Firma yöneticisi ve personel rollerinden farklı `PlatformAdmin` rolü ve ayrı cookie oturumu gerekir. Her veri işlemi yeniden parola doğrular. CSRF, deneme sınırı ve denetim kaydı uygulanır.

Render güvenli ortam değişkenleri:

- `PlatformAdmin__Email`: kullanıcı için seçilen geliştirici e-postası. Mevcut bir firma hesabıyla aynı olamaz.
- `PlatformAdmin__Password`: güçlü, yalnız sahibinde bulunan parola. Repo, APK, yayın notu veya loga yazılmaz. İlk oluşturma içindir; mevcut hesabın parolasını sessizce sıfırlamaz.
- `DeveloperTools__ReadOnlyConnectionString`: aşağıdaki ayrı rolün TLS bağlantısı. Tanımlanmazsa sorgu işlevi kapalı kalır; ana uygulama bağlantısına geri dönmez.

GitHub Render uygulamasının bu repoya erişimi doğrulandıktan sonra `main` deploy edilir. `/health/ready` ve `/api/status` sürüm 3.4.0 doğrulanır. Yeni migration'lar eklemeli niteliktedir; geri dönüşte önce eski uygulama imajı kullanılır, üretim verisi bulunan sütunlar/table otomatik silinmez. Üretim migration'ından önce sağlayıcı yedeği/geri dönüş noktası alınmalıdır.

## PostgreSQL konsol rolü

Aşağıdaki örnek doğru üretim veritabanında yetkili operatör tarafından uygulanır. Parola sağlayıcının güvenli rol yönetimi veya psql `\password valem_diagnostics` ile atanır; SQL dosyasına yazılmaz.

```sql
CREATE ROLE valem_diagnostics LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
GRANT CONNECT ON DATABASE your_database TO valem_diagnostics;
GRANT USAGE ON SCHEMA public TO valem_diagnostics;
GRANT SELECT ON TABLE public."Companies", public."Branches",
  public."Customers", public."Vehicles", public."ParkingTickets", public."Payments",
  public."RegistrationRequests", public."UserBranchMemberships", public."Notifications",
  public."UserEntitlements", public."AuditEntries", public."PlatformAuditEntries",
  public."RequestFailures", public."__EFMigrationsHistory"
  TO valem_diagnostics;
ALTER ROLE valem_diagnostics SET default_transaction_read_only = on;
```

Veritabanı adını gerçek adla değiştirin. Tüm tablolara veya varsayılan gelecekteki tablolara SELECT verilmez: kimlik tabloları, parola hash'leri, OTP ve oturum kayıtları hariçtir. Rolün PUBLIC veya üyelik yoluyla CREATE/yazma yetkisi varsa konsol açılmaz; önce o yetkinin etkisi incelenir. Ana bağlantı hesabı sorgu hesabı olarak kullanılmaz.

## JSON aktarım sınırları

Bu araç tam PostgreSQL yedeği değildir: aynı firmaya ait müşteri, araç, vale fişi ve ödeme verilerini taşır. Kimlik, parola, firma/şube yapılandırması ve kullanıcı hesapları taşınmaz. Tablo başına 5.000 satır, dosya başına 20 MB sınırı vardır. Mevcut kayıtlar değiştirilmez. Önizleme transaction'ı geri alınır; onay 10 dakika içinde aynı dosya ve kullanıcıya bağlıdır. Yanlış tenant veya ilişki tüm aktarımı geri alır. Felaket kurtarma için sağlayıcının tam yedek/PITR sistemi kullanılır.

## Gizlilik ve tanılama

Telefon, doğum tarihi, şehir, hakkımda ve profil fotoğrafı isteğe bağlıdır. Fotoğraf boyutlandırılır, yeniden JPEG kodlanır; EXIF yönü uygulanır ve konum metadata'sı aktarılmaz. Özel tema resmi telefonda kalır, tema seçimi API üzerinden saklanır. Play veri güvenliği beyanı ve gizlilik metni bu profil alanlarını da kapsamalıdır.

Tanılama tablosu tarih, HTTP durumu, rota kalıbı, kullanıcı/firma kimliği ve destek izini kaydeder; istek gövdesi, parola ve SQL metni kaydetmez. Bu tanılama kayıtları 30 gün tutulur. Çevrimdışı cihaz hataları ve yerel crash'ler sunucuya ulaşmadığından bu tabloda eksiksiz cihaz telemetrisi olduğu iddia edilmez.

Tasarım dayanakları: [Android izinleri azaltma](https://developer.android.com/privacy-and-security/minimize-permission-requests) ve [OWASP hassas işlemlerde yeniden doğrulama](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html).

# VALEM 3.6.0 doğrulama

30 Eylül 2026, yerel Windows build; Android 14 Pixel 7 emülatörü.

- API regresyon: 116 test geçti, hata/atlanan test yok. SMS numara normalizasyonu, doğrulanmamış numarayla girişin reddi, tek kullanımlık kod, Authenticator zorunluluğu, numara/amaç kapsamı, e-posta değişiminde parola ve yeni adres kanıtı, telefon değişiminde doğrulamanın iptali ve premium görünüm izinleri dahil.
- Android Release APK/AAB: uyarıları hata sayarak derlendi. Kalıcı upload sertifikasıyla imzalandı; APK v2/v3 ve AAB imzası, paket/sürüm ve 22 yerel kütüphanenin 16 KB hizalaması doğrulandı.
- PostgreSQL 18: yalnız bu iş için oluşturulan boş yerel veritabanına sekiz migration uygulandı. EF modelinde bekleyen değişiklik yok. VDS ön kontrolünde benzersiz doğrulanmış telefon indeksini engelleyecek mükerrer numara bulunmadı.
- API/istemci ve mobil bağımlılıklar NuGet güvenlik verileriyle tarandı; bilinen açık bildirilmedi. Bu tarama uygulamada hiç açık olmadığı garantisi değildir.
- Emülatör: demo giriş, eğitim ileri/çarpı, eğitim sırasında menünün devre dışı kalması ve kapanınca geri açılması; araç ayrıntısından seçili sekmeye yeniden basarak köke dönüş; raporlara ve Daha Fazla'ya geçiş; Excel önizleme; ayarlarda açık/koyu mod; büyütülen görsel kılavuz kontrol edildi.
- UI test projesi derlendi. Appium ve fiziksel cihaz testleri çalıştırılmadı; emülatör denemeleri bunların yerine başarılı gösterilmedi.
- Gerçek SMS ve Google Play ödemesi test edilmedi: SMS sağlayıcısı ve Play Console geliştirici/ürün/servis hesabı kurulumu yok. SMS kapalıdır; ödemeler sahte başarı vermez ve sunucu doğrulaması gerektirir.

Her gerçek cihazın ekran boyutu/font ölçeği ayrıca kullanıcı kabul testinde kontrol edilmelidir. Eğitim görüntüleri ve UI dump'ları yerel ignored `artifacts/android-ui-3.6.0` klasöründedir; kişisel hesap parolaları/logları yayın varlığı değildir.

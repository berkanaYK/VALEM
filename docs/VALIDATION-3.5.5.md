# VALEM 3.5.5 doğrulama — 30 Eylül 2026

- Parola kuralı ihlali sonrası geçerli kodun tüketilmesi yeni regresyon testinde önce tekrar üretildi; düzeltmeden sonra test geçti.
- Backend testleri: 103 başarılı, 0 başarısız. Kapsam: parola reddi sonrası aynı kodla tekrar deneme, başarılı sıfırlamadan sonra kodun yeniden kullanılamaması, yanlış kod, yeni kodun önceki kodu geçersiz kılması, 15 dakika süresi, süresi dolan kod, hesap bazında hatalı kod kilidi ve ortak IP kotasının kullanılmaması.
- API Release ve mobil UI test paketi uyarılar hata kabul edilerek derlendi. EF Core bekleyen model değişikliği olmadığını doğruladı.
- Android Release APK/AAB üretildi. Kalıcı RSA 4096 yayın anahtarı kullanılır; APK v2/v3 imzası, AAB sertifikası, uygulama kimliği, debug/cleartext/backup ayarları ve 22 yerel kitaplığın 16 KB hizalaması paket doğrulama betiğiyle kontrol edilir.
- Android 14 Pixel 7 emülatöründe uygulama kuruldu/açıldı. Giriş Yap metni, parola görünürlük durumu, göz simgesinin erişilebilirlik açıklaması, oturum anahtarının açık/kapalı durumu, bağlantı ayarlarının kaldırılması ve destek sayfasına geçiş kontrol edildi.
- Destek e-postası yüklü Gmail'in SENDTO oluşturma ekranını doğru alıcı/konu/içerikle açtı. Emülatörde Gmail geçici kapatıldığında aynı düğme doğru Gmail web oluşturma adresini Chrome'a iletti; Gmail daha sonra yeniden etkinleştirildi. Test sırasında destek e-postası gönderilmedi.
- Fiziksel telefon bağlı olmadığı için gerçek cihaz Appium kalite kapısı çalıştırılmadı. Birden fazla e-posta uygulamasının seçim ekranı, telefonun varsayılan uygulama tercihlerine bağlıdır; fiziksel cihazda tekrar denenmelidir.

Üretim dağıtımı ve HTTP kontrollerinin çıktıları yerel `artifacts/deploy` klasöründe tutulur. Bunlar dağıtım öncesi testleri üretim testi olarak göstermemek için ayrı kaydedilir.

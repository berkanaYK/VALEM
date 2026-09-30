# VALEM 3.6.1 doğrulama

30 Eylül 2026, Windows yerel derleme; Android 14 Pixel 7 emülatörü.

- Eski 3.6.0 hatası tekrar üretildi: ayarlarda koyu seçiliyken profil açıldığında sunucunun Sistem tercihi uygulandı ve arka plan açıldı; ayarlar seçimi ise Koyu kaldı.
- API regresyonunda 116 test geçti; başarısız veya atlanan test yok. UI test projesi uyarısız derlendi. EF modelinde bekleyen değişiklik yok; yeni migration gerekmedi.
- Release APK/AAB uyarıları hata sayarak derlendi ve mevcut upload anahtarıyla imzalandı. APK v2/v3, AAB imzası, paket/sürüm ve 22 yerel kütüphanenin 16 KB hizalaması doğrulandı.
- İmzalı 3.6.1 yüklenerek `scripts/check-android-theme-navigation.ps1` çalıştırıldı. Koyu ve açık modda ayarlar, profil ve beş ana sekme dahil 16 ekran kontrolü geçti. Hesaba kaydet düğmesine basılmadı. Ayarlara dönüldüğünde seçim alanının güncel modu gösterdiği doğrulandı.
- Sistem modu seçilerek Android gece modu açılıp kapatıldı; uygulama iki değişikliği de takip etti. Seçili Koyu mod ile uygulama tamamen kapatılıp yeniden açıldı; giriş ekranı ve yeniden açılan demo koyu kaldı. Demo eğitimi kapanınca da renkler korundu.
- Profil başlığı ve koyu profilde yazı okunurluğu görsel olarak kontrol edildi. Ekran görüntüleri ve taze UI ağaçları ignored `artifacts/android-ui-3.6.1` klasöründedir.
- Galeri seçiminin modu değiştirmesi ve eski oturum yanıtının yeni oturuma uygulanması kod incelemesinde düzeltildi; bu iki yol için ayrı fiziksel cihaz/ağ gecikmesi testi çalıştırılmadı.

Fiziksel cihaz ve Appium testleri çalıştırılmadı. Bu kontroller bütün cihazlarda veya uygulamanın bütün iş akışlarında hata bulunmadığı garantisi değildir. Gerçek SMS ve Google Play ödeme testleri sağlayıcı/Play Console kurulumu olmadığı için yapılmadı; mevcut kapalı/kurulum bekleyen davranış korundu.

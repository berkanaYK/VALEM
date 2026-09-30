# VALEM 3.7.0 doğrulama

30 Eylül 2026. Windows yerel Release derlemesi; Android 14 Pixel 7 emülatörü.

- API: 116 test geçti, başarısız/atlanan yok. Tenant/nesne yetkilendirmesi, premium sınırları, hesap güvenliği ve profil alanları dahil mevcut regresyonlar çalıştırıldı. Profil testi Copper tercihinin sunucu yanıtında korunmasını da doğrular.
- API ve mobil projeler doğrudan/geçişli NuGet bağımlılıklarıyla tarandı; bilinen açık bildirilmedi. Aktif üretim saldırı/yük testi yapılmadı; bu sonuç bütün güvenlik açıklarının bulunmadığı garantisi değildir.
- EF model kontrolü: bekleyen değişiklik yok; yeni veritabanı migration gerekmiyor.
- APK/AAB uyarıları hata sayarak yayımlandı. Kalıcı upload sertifikasıyla imzalar, paket kimliği, 3.7.0/build 27 sürümü ve 22 yerel kütüphanenin 16 KB hizalaması doğrulandı. Önceki imzalı uygulama üzerine güncelleme kurulumu başarılı.
- İmzalı son APK'da iki modda 16 ekran kontrolü geçti: ayarlar, profil, beş ana sekme ve ayarlara dönüş. Kaydet düğmesine basılmadı; seçim ve renkler korundu. Aynı kontroller %130 Android yazı ölçeğinde, alt menü simge/metin geometrisini de denetleyerek çalıştırıldı.
- Giriş, ana sayfa, araç kabul formu ve genişletilen araç alanları, destek ve Daha Fazla bölümleri normal/%130 yazı ölçeğinde görsel olarak incelendi. Büyük yazıda alt menü adlarının kısalması düzeltildi; son pakette adlar tam okunur ve simgelerle örtüşmez.
- Sistem teması seçiliyken Android gece modu açılıp kapatıldı: koyu RGB16,29,42 ve açık RGB250,247,242 doğrulandı. Koyu mod seçilerek uygulama zorla kapatılıp yeniden açıldı; giriş ekranı koyu kaldı. Emülatör yazı ölçeği 1.0'a ve gece modu kapalı duruma geri getirildi.
- Ana/ikincil metinler ve dört vurgu rengindeki birincil butonlar için 12 renk eşleşmesinin hesaplanan kontrastı 4.5:1 üstünde; en düşük 5.05:1. Bu kontrol fotoğrafın bütün pikselleri için kontrast garantisi değildir; hero alanına koyu katman ve açık metin uygulanır.
- Eğitim yeni yerleşimde tetiklendi ve kapatılınca menüler kullanılabilir kaldı. Telefon ana ekranı için üretilen yuvarlak V simgesi görsel olarak kontrol edildi; SVG ve Android stil XML dosyaları geçerli.
- UI test projesi uyarısız derlendi. Appium ve fiziksel telefon testleri çalıştırılmadı. Gerçek SMS/Google Play ödemesi sağlayıcı/Play Console kurulumu olmadığından denenmedi.

Kanıtlar ignored `artifacts/android-ui-3.7.0`, `theme-navigation-release-3.7.0.log`, `contrast-3.7.0.txt`, derleme/imza/test ve audit çıktılarındadır. Farklı ekranlar, Android sürümleri ve daha yüksek yazı ölçekleri ayrıca kullanıcı kabul testinde kontrol edilmelidir.

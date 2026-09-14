# VALEM 3.5.1 — Android build 20

## Yenilikler

- Ayarlar ve Yardım bölümlerine sekiz bölümlük görsel kullanım kılavuzu eklendi.
- Kılavuz; hesap oluşturma, ana sayfa, araç kabulü, teslim süreci, profil ve temalar, Deneme/Sınırsız farkı, güvenlik ve destek konularını adım adım anlatır.
- Sayfalar kaydırılabilir kartlar, bölüm göstergesi, önceki/sonraki düğmeleri ve kullanıcının güncel paket/kota bilgisiyle dinamik çalışır.
- Kılavuzdan ilk kullanım eğitim baloncukları yeniden başlatılabilir.
- Giriş ekranı, Ayarlar ve Yardım bölümüne ayrı İletişim ve Destek bağlantısı eklendi.
- Destek adresi `berkanaz.aydin8@gmail.com` olarak görünür; kullanıcı adresi kopyalayabilir veya sürüm/cihaz bilgisi eklenmiş hazır e-posta taslağı açabilir.
- Destek ekranı hata bildiriminin nasıl yazılacağını açıklar ve parola, giriş kodu, Authenticator kodu veya ödeme bilgisinin gönderilmemesi gerektiğini belirtir.

## Doğrulama

- Tüm EF migration zinciri temiz PostgreSQL 17 veritabanına başarıyla uygulandı.
- API testleri, solution ve Android Release derlemesi çalıştırıldı.
- APK/AAB kalıcı VALEM upload anahtarıyla imzalanıp imza, sertifika, paket kimliği, target SDK ve 16 KB hizalama kontrollerinden geçirildi.

Canlı sunucu taşıması yapılana kadar mevcut Render API 3.3.0 çalışmaya devam eder. Kota, satın alma ve premium sunucu özellikleri VDS üzerinde API 3.5.1 dağıtılınca etkinleşir.

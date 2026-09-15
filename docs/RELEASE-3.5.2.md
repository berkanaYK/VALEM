# VALEM 3.5.2 — Android build 21

Bu sürüm yayın öncesi son kararlılık ve güvenlik kontrollerini içerir.

- E-posta doğrulama sonuç sayfası katı içerik güvenliği politikasıyla uyumlu harici stil dosyasını kullanır.
- E-posta doğrulama bağlantısı otomatik istek yoğunluğuna karşı sınırlandırılır.
- Rastgele ve eşleşmeyen adres istekleri tanılama veritabanını gereksiz kayıtlarla doldurmaz.
- Platform yöneticisi parolası güvenli sunucu yapılandırmasından değiştirildiğinde mevcut hesapta da yenilenir ve eski parola geçersizleşir.
- Kaynak biçimlendirme kapısı temizlendi.

Doğrulama kapsamında 89 API testi, Release çözüm derlemesi, temiz PostgreSQL migration/seed, gerçek HTTP sağlık-yetki-demo akışı, NuGet açık taraması, Docker imaj derlemesi ve Android imzalama/paket kontrolleri çalıştırıldı.

Canlı sunucu taşıması yapılana kadar mevcut Render API 3.3.0 çalışmaya devam eder. Kota, satın alma ve premium sunucu özellikleri VDS üzerinde API 3.5.2 dağıtılınca etkinleşir.

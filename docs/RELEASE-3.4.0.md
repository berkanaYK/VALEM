# VALEM 3.4.0 — Android build 18

- Ayarlar artık fotoğraf ekleme/kaldırma, telefon, doğum tarihi, şehir ve hakkımda alanlarını içeren profil ekranına açılır. İsteğe bağlı bilgiler silinebilir.
- Firma oluşturma, bağımsız kişisel hesap ve mevcut firmaya kodla katılma ayrı seçeneklerdir. Firma katılımında yönetici onayı ve veri ayrımı korunur.
- Hesap açmadan inceleme, örnek verili ve sunucuda yazmaya kapalı demo oturumu başlatır.
- Android metin alanlarının ayrı koyu zemini kaldırıldı. Resimli tema, renk seçimi ve Android 12+ bulanıklık desteği; tema ekranına yeniden girişte yenileme düzeltildi.
- Müşteriye anlaşılır Türkçe hatalar; web yönetiminde destek kayıt numarasıyla tanılama araması eklendi.
- Geliştirici bölümünde VALEM yazısına çift dokunma, ayrı platform hesabıyla korunan veritabanı araçlarını açar. Firma işlem verileri için JSON dışa aktarma ve önizlemeli, mevcut kayıtları koruyan içe aktarma bulunur.
- SQL konsolu ayrı, yalnız okuma yetkili PostgreSQL hesabı ister; 5 saniye, 200 satır ve 50 sütunla sınırlıdır. Ana veritabanı parolası veya geliştirici parolası APK içine konmaz.

## Dağıtım gereksinimi

Bu paket API 3.4.0 ve yeni migration'larla birlikte kullanılır. Render GitHub kaynağını kopyalayamadığı sürece yeni demo/profil/destek işlevleri canlı ortamda hazır değildir. Platform hesabı ve SQL bağlantısı sunucuda ayrıca yapılandırılmalıdır. Bu koşullar doğrulanana kadar yayın taslakta tutulur.

## Paketler

`VALE.apk` doğrudan Android kurulumu; `VALE.aab` Google Play Console yüklemesi içindir. Her ikisi mevcut kalıcı VALEM anahtarıyla imzalanır. Dosya bütünlüğü `SHA256SUMS.txt` üzerinden doğrulanabilir. APK imzası bilinmeyen kaynak veya Play Protect tanınırlık uyarısının kaldırılacağını garanti etmez.

## Doğrulama

84 API testi geçti. Ayrı yerel PostgreSQL üzerinde migration, demo yazma engeli, kayıt/giriş, platform oturumu, SQL okuma/yazma ve kimlik tablosu erişim sınırları, CSRF, dışa aktarma ve tanılama kontrol edildi. Android Release derlemesi ve APK/AAB imzası ile 16 KB native hizalama denetlendi. Emülatör kontrolleri fiziksel telefon testi yerine geçmez.

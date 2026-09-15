# VALEM 3.5.4 — Android build 23

Bu sürüm yeni hesapların benzersiz kullanıcı adı ve güçlü parola oluşturmasını zorunlu hale getirir. Parola 6-20 karakterdir ve büyük harf, küçük harf, rakam ile özel karakter içerir. Parolalar ASP.NET Core Identity tarafından tek yönlü, tuzlanmış özet olarak saklanır.

Ana giriş ekranı e-posta veya kullanıcı adını kabul eder. Parola alanındaki göz düğmesi görünürlüğü değiştirir; kayıt ekranındaki iki parola alanında da aynı kontrol vardır. E-posta koduyla giriş ayrı seçenek olarak korunmuştur. Yeni hesaplar parola veya kodla giriş yapmadan önce gönderilen bağlantıyla e-posta adresini doğrulamalıdır.

Giriş ve kayıt alanları tema uyumlu çerçeve ve odak vurgusuyla daha belirgin hale getirilmiştir. Üretim kontrolleri Render yerine kalıcı `api.valemyonetim.com` VDS adresini hedefler.

# VALEM 3.5.5 — Android build 24

- Destek e-postası Android'deki e-posta uygulamasında, alıcı adresi hazır olarak açılır. Uygulama yoksa tarayıcıda Gmail yazma ekranına geçilir. Uygulama seçimi ve varsayılan tercih Android tarafından yönetilir. E-posta uygulamasında hesap kurulu değilse doğrudan Tarayıcıdan E-posta Gönder seçeneği kullanılabilir.
- İletişim ve Destek ana ekrana ve uygulama menüsüne eklendi; demo kullanıcıları da erişebilir.
- Giriş ekranından Bağlantı ayarları kaldırıldı. Ana düğme Giriş Yap olarak sadeleştirildi.
- Parola koşullarına uymayan yeni parola artık sıfırlama kodunu tüketmez. Parola tekrarı, kod kontrolü ve gönderim sırasında işlem kilidi eklendi. Yeni kod gönderildiğinde önceki kod geçersiz olur; son kod 15 dakika kullanılabilir.
- Parola kurtarmadaki ortak IP istek sınırı kaldırıldı. Beş hatalı kod denemesi sonrasında yalnız ilgili hesabı geçici kilitleyen koruma devam eder.
- Parola göz simgesi açık/kapalı göz olarak değişir; oturumu açık tut düğmesi 240 ms yumuşak animasyonla geçiş yapar.

Kaynak dalları kullanıcının isteğiyle pazartesi toplu gönderilir. Bu tarihe kadar bu sürümün kaynakları yerel dal ve VDS'deki Git bundle ile saklanır; GitHub yayın etiketi mevcut main commitini hedefler. İmzalı uygulama paketleri bu sürümün yerel kaynaklarından oluşturulmuştur.

VALEM 3.3.2 — Android build 17

- Kalıcı RSA 4096 bit sertifikayla imzalanan APK ve Google Play yüklemesi için AAB.
- Release yayınında test/debug imzasına dönüş kaldırıldı; eksik imzalama secret'ı yayını durdurur.
- APK/AAB sertifika eşleşmesi, APK v2 imzası, manifest ve 16 KB native ELF/ZIP hizalama kontrolleri.
- Şifresiz HTTP trafiği ve uygulama yedeği kapatıldı.
- Kayıt akışını derlenemez hale getiren hata ve test ortamı düzeltmeleri.
- E-posta OTP/TOTP hatalı deneme sayacı, hesap kilidi ve başarılı girişte sayaç sıfırlama düzeltmeleri.

`VALE.apk` doğrudan Android kurulumu içindir. `VALE.aab` Play Console yüklemesi
içindir; telefona doğrudan kurulmaz. `upload-certificate.pem` yalnız herkese açık
upload sertifikasıdır. Dosya bütünlüğünü `SHA256SUMS.txt` ile kontrol edebilirsiniz.

Kalıcı sertifika SHA-256:
`A0:6C:75:FC:41:B5:CE:D1:BF:DA:2E:D4:FB:CA:CF:5D:1B:46:DC:2D:00:8C:06:9D:42:B1:0A:38:AA:99:B0:59`

Önceki test APK'sı başka anahtarla imzalandıysa Android üzerine güncellemeye izin
vermez. Veri ve hesap erişimini güvenceye almadan eski uygulamayı kaldırmayın.
Google'ın oluşturduğu Play App Signing anahtarı seçilirse Play Store APK'sının
sertifikası bu GitHub APK'sından farklı olur; iki kanal arasında doğrudan
güncelleme beklenmemelidir.

Kalıcı imza, mağaza dışı kurulumdaki “bilinmeyen kaynak / Play Protect bu uygulamayı
tanımıyor” uyarısını kaldırma garantisi değildir. Bu sürüm Play Protect'i kapatmaz
ve Google tarafından onaylanmış olarak sunulmaz. Kullanıcıların Play Store
üzerinden kurulum yapabilmesi için Play Console test/yayın süreci tamamlanmalıdır.

Fiziksel Android cihaz testi bu yayın hazırlığında çalıştırılmadı.

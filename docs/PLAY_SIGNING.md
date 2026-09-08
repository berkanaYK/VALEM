# Play imzalama ve dağıtım

Android uygulama kimliği: `com.berkanayk.vale`. Mobil sürüm 3.3.2 / build 17.
API sözleşmesi 3.3.1 ile devam eder.

## Kalıcı anahtar

Upload alias: `vale-play-upload`, RSA 4096 bit, 10000 gün geçerlilik.
GitHub Actions secret'ları:

- `VALE_ANDROID_KEYSTORE_B64`
- `VALE_ANDROID_STORE_PASSWORD`
- `VALE_ANDROID_KEY_ALIAS`
- `VALE_ANDROID_KEY_PASSWORD`

Anahtar ve parolalar repo/Release içine eklenmez. Yeni anahtar üretme betiği
mevcut keystore üzerine yazmaz. `-GeneratePasswords` seçeneği PowerShell 7.2+
Windows üzerinde rastgele parolalar üretir, dizin ACL'sini kullanıcıyla sınırlar
ve `signing-passwords.clixml` içinde Windows DPAPI ile şifreler. **Bu dosya yalnız
oluşturulduğu Windows hesabı/makine üzerinde açılabilir; taşınabilir yedek değildir.**
Makine yenilenmeden önce parolaları güvenli parola yöneticisine, keystore'u da
ayrı güvenli çevrimdışı yedeğe aktarın. Sadece JKS kopyası parolasız kullanılamaz.

`scripts/publish-android-signed.ps1` APK ve AAB'yi aynı anahtarla imzalar.
Parolalar MSBuild'e komut satırı değeri olarak değil `file:` referansıyla verilir;
AAB'de `env:` desteği bulunmadığından geçici dosyalar kullanılır. CI bu dosyaları
ve geçici keystore'u `finally` içinde siler.
`scripts/verify-android-package.ps1` imzaları, sertifika eşleşmesini, manifesti,
64 bit native kütüphanelerin 16 KB ELF hizalamasını ve APK ZIP hizalamasını denetler.

## Play Console

1. Uygulamayı aynı application ID ile oluşturun; Play App Signing'i yapılandırın.
2. Release'teki `VALE.aab` dosyasını önce internal testing kanalına yükleyin.
3. Google ayrı app signing key oluşturursa FCM/Google servislerinde istenen
   SHA-1/SHA-256 değerleri için Play Console'daki **app signing certificate**
   değerlerini kullanın; upload certificate ile karıştırmayın.
4. App Bundle Explorer/ön yayın raporunda cihaz ve 16 KB uyumluluğunu doğrulayın.
5. Gizlilik politikası, Data safety, izin gerekçeleri, içerik derecelendirmesi,
   hesap silme yolu ve gerekiyorsa hesap türüne bağlı test koşullarını tamamlayın.

GitHub APK'sı upload anahtarıyla imzalanır. Play Store dağıtımı Google'ın app
signing anahtarını kullanırsa bu kanallar farklı imzalara sahiptir. Aynı cihazda
kanallar arası geçişte imza uyuşmazlığı yaşanabilir. Tek dağıtım kanalı olarak
Play Store tercih edildiğinde kullanıcılar Play üzerinden güncelleme alır.

## Telefon uyarısı

“Bilinmeyen kaynak” mağaza dışı kurulum izniyle, “Play Protect bu uygulamayı
tanımıyor” Google'ın uygulamayı henüz tanımamasıyla ilgilidir. Bir sertifika veya
APK yaması bunların kaldırılmasını garanti etmez. Android işletim sistemi güvenlik
yamaları cihaz üreticisi/Google tarafından sağlanır; uygulama APK'sına eklenmez.
Play Protect devre dışı bırakılmaz. Yanlış zararlı uygulama sınıflandırması varsa
Google'ın itiraz süreci kullanılır; tanınmayan uygulama mesajı tek başına böyle
bir sınıflandırma değildir.

Kaynaklar:

- [Google Play App Signing](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en)
- [Play Protect uyarıları](https://developers.google.com/android/play-protect/warning-dev-guidance)
- [MAUI APK/AAB imzalama](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0)
- [Android 16 KB gereksinimi](https://android-developers.googleblog.com/2025/05/prepare-play-apps-for-devices-with-16kb-page-size.html)

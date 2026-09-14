# Google Play satın alma kurulumu

VALEM, `valem_premium_lifetime` kimlikli tek seferlik ve tüketilmeyen bir ürün kullanır. Uygulamadaki `$5.99` yalnızca yedek gösterimdir; gerçek fiyat ve para birimi Google Play Console'daki ülke fiyatlandırmasından okunur.

## Play Console

1. `com.berkanayk.vale` uygulamasında **Para kazanma > Ürünler > Uygulama içi ürünler** bölümünü açın.
2. Ürün kimliğini `valem_premium_lifetime` olarak oluşturun, etkinleştirin ve temel fiyatı 5,99 USD belirleyin.
3. Google Cloud'da yalnız Android Publisher API erişimi olan bir servis hesabı oluşturun.
4. Play Console **Kullanıcılar ve izinler** bölümünde bu servis hesabına sipariş ve abonelik görüntüleme/yönetme için gereken en dar izni verin.
5. JSON anahtarını kaynak koduna eklemeyin. Sunucuda `BILLING_SERVICE_ACCOUNT_JSON` gizli değişkenine tek satır JSON olarak kaydedin ve `BILLING_GOOGLE_PLAY_ENABLED=true` yapın.

## Sunucu davranışı

- Mobil istemci ödeme sonucunu tek başına geçerli saymaz; satın alma belirteci API tarafından Google Play ile doğrulanır.
- Belirtecin özeti veritabanında benzersizdir ve başka bir VALEM kullanıcısına aktarılamaz.
- Ham belirteç Data Protection ile şifrelenir. Sunucu satın almayı 12 saatte bir yeniden doğrular; iade veya iptal Google Play tarafından bildirildiğinde yetki kapanır.
- Google Play geçici olarak yanıt vermezse daha önce doğrulanmış kullanıcının erişimi kesilmez.
- Test hesabı VPS ortamında `BILLING_TEST_PREMIUM_EMAIL_0` ile tanımlanır.

Gerçek satın alma testi için APK'yı elle kurmak yerine Play Console kapalı test kanalından yükleyin ve test kullanıcısını lisans testçilerine ekleyin.

# VALEM 3.5.0 — Android build 19

## Yenilikler

- Kayıt ekranında ve kullanıcıya özel ilk oturumda adım adım kullanım rehberi açılır. Rehber Ayarlar ekranından yeniden başlatılabilir.
- Ücretsiz sürüm kullanıcı başına 50 araç kaydıyla sınırlıdır. Ödeme veya teslim içermeyen eski kayıt silindiğinde kota yeniden açılır.
- `valem_premium_lifetime` tek seferlik Google Play ürünü sınırsız araç kaydı, premium resimli temalar, galeriden arka plan ve profil çerçevelerini açar.
- Satın alma belirteci mobil cihazda güvenilir sayılmaz; API Google Play üzerinden doğrular, kullanıcı hesabına bağlar ve iade durumunu düzenli yeniden kontrol eder.
- Satın alma ekranı ücretsiz/Sınırsız karşılaştırmasını, gerçek Play fiyatını, kalan kotayı ve geri yükleme seçeneğini gösterir. Satın alan kullanıcıda ödeme düğmesi gizlenir.
- Profil fotoğrafı, isteğe bağlı telefon/doğum tarihi/şehir/hakkımda alanları; altın, neon ve karbon profil çerçeveleri; hazır araç temaları ve galeriden özel arka plan tek ekranda yönetilir.
- Metin, parola ve seçim alanları tema renginde yumuşak bir yüzeyle görünür hale getirildi. Görsel temalarda kart ve yazı kontrastı otomatik korunur.
- Araç marka ve model listeleri Türkçe alfabetik sıralanır. Model alanı marka seçilene kadar kapalıdır ve sebebi Türkçe yardımcı metinle açıklanır.
- Kullanıcıya HTTP/hata kodu veya İngilizce doğrulama metni gösterilmez. Teknik ayrıntı sunucu loglarında kalır.
- Platform yönetim paneli kullanıcıların Deneme/Sınırsız durumunu ve toplam satın alan kullanıcı sayısını gösterir.

## Dağıtım gereksinimleri

Bu APK, eski API ile temel araç ve profil işlemlerini sürdürür; kota, premium ve yeni profil çerçevesi için API 3.5.0 migration'ıyla birlikte dağıtılmalıdır. Google Play ürünü ve servis hesabı kurulana kadar gerçek ödeme kapalı kalır. `memeloialimon@gmail.com` Render yapılandırmasında ödeme gerektirmeyen test hesabı olarak tanımlanmıştır.

## Doğrulama

- 87 API/yetkilendirme/regresyon testi geçti.
- API ve Android Release derlemeleri uyarısız tamamlandı.
- API ve mobil bağımlılık taramasında bilinen güvenlik açığı raporlanmadı.
- Google Play Billing izni birleşik Android manifestinde doğrulandı.
- APK ve AAB kalıcı VALEM upload anahtarıyla imzalanıp imza/sertifika ve 16 KB uyumluluk denetiminden geçirilir.

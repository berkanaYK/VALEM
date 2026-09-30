# VALEM otomobil odaklı görünüm

Kullanıcı 30 Eylül 2026'da beşinci tasarım konseptini seçti. Uygulama .NET MAUI mimarisi ve mevcut gezinme/tenant yetkilendirmesini korur.

Koyu: sayfa #101D2A, kart #172837, alan #203546, yazı #F6F2EC, ikincil yazı #BAC7D0, bakır #D89C65. Açık: sayfa #FAF7F2, kart #FFFFFF, alan #F1EAE1, yazı #142A3B, ikincil yazı #536575, bakır #92552F. Açık moddaki bakır koyulaştırılarak beyaz buton metnine kontrast sağlanır. Koyu mod butonları lacivert yazı kullanır. Opak veri yüzeyleri seçilen galeri/hazır fotoğraftan bağımsız okunur.

Ana ekranın fotoğrafı uygulamaya paketlenir; uzak görsel sunucusuna bağımlı değildir. Üretim dosyası: `src/VALE.Mobile/Resources/Images/valem_hotel_hero.png`. Yerleşik image_gen aracıyla üretildi. Prompt: “Production background photograph for VALEM valet parking mobile app header, midnight navy and warm copper hospitality interface. Unbranded dark luxury sedan arriving at refined hotel valet entrance at dusk, rear three-quarter view, warm amber architectural lighting, navy shadows, negative space for greeting; no people, typography, logo, watermarks or readable license plate.”

Bu fotoğraf gerçek bir müşteri aracı değildir. Araç satırları uygulamanın gerçek kayıt verilerini ve ortak araç simgesini gösterir; örnek tasarım görsellerindeki otomobil fotoğrafları gerçek kayıt fotoğrafı gibi eklenmedi.

Mevcut SVG simgesi kodla düzenlenerek adaptif renkli ve Android tek renk karşılıkları oluşturuldu. Logo geometrisi merkezde tutulur; farklı launcher kırpmaları için foreground güvenli alan kullanılır. Görsel ve simge projededir; oluşturma klasöründeki dosyaya çalışma zamanında bağımlılık yoktur.

Üç sütunlu operasyon özetinde ciro alt satıra ayrılır: uzun tutarlar dar sütuna sıkışmaz; ciro yetkisi olmayan kullanıcıya gösterilmez. Şube seçimi, kayıt kotası ve eğitim hedefleri korunur. Sistemi takip eden, koyu ve açık modların hepsi desteklenir; 3.6.1 cihaz tercihi sürekliliği korunur.

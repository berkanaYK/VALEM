# VALE 3.1

VALE 3.1, tek firma varsayımını kaldırıp sistemi gerçek çoklu müşteri / çoklu firma yapısına geçirir. Her operasyon `Company -> Branch -> User` kapsamındadır.

## Firma ve şube modeli

- `Company.Code` sistem genelinde benzersiz firma kimliğidir.
- `Branch.Code` yalnızca kendi firması içinde benzersizdir. Farklı firmalar aynı şube kodunu kullanabilir.
- Kullanıcı, araç ve müşteri verileri `CompanyId` ile ayrılır.
- Yönetici ve firma sahibi yalnızca kendi firmasının şubelerini, personelini, raporlarını ve denetim kayıtlarını görebilir.
- Eski tek-firma verileri ilk v3.1 başlangıcında `VALE / VALE Ana Firma` tenant'ına otomatik bağlanır; mevcut kayıtlar silinmez.

## Kayıt akışları

### Firma sahibi / yönetici

Yeni kullanıcı kayıt ekranında `Firma sahibi / yönetici` seçer ve firma adı/kodu ile ilk şube bilgilerini girer. Sistem:

1. Firmayı oluşturur.
2. İlk şubeyi oluşturur.
3. Hesabı aktif eder.
4. `Owner` ve `Admin` rollerini atar.
5. Kullanıcı hemen giriş yapabilir.

### Mevcut firmaya personel

İki yöntem vardır:

- Yönetici tarafından üretilen süreli `VALE-...` davet kodu.
- Firma kodu ile firmayı bulup aktif şubelerden seçim yapmak.

Personel hesabı `Pending` başvuru olarak açılır. Onay bildirimi yalnızca aynı firmadaki yetkili yöneticilere düşer. Yönetici rol seçerek onaylayabilir veya açıklama ile reddedebilir.

## Bildirimler

v3.1 kalıcı uygulama içi bildirim merkezi ekler:

- yeni personel başvurusu,
- başvuru onay / ret sonucu,
- araç teslim için istendi bildirimi.

Bildirimler kullanıcı/firma bazında saklanır, tek tek veya topluca okundu işaretlenebilir.

> Android sistem push bildirimi (FCM) için ayrıca Firebase proje kimliği ve sunucu kimlik bilgileri gerekir. Bu sırlar repoya yazılmaz. v3.1 herhangi bir sahte Firebase anahtarı eklemez.

## Canlı park ücreti

Açık araç kayıtlarında `AmountDue`, her API yenilemesinde mevcut zamana kadar `IFeeCalculator` ile tekrar hesaplanır. Böylece kullanıcı ödeme ekranına gelmeden önce güncel tahmini tutarı görür. Teslim sırasında aynı hesaplayıcıyla son tutar sabitlenir ve ödeme kaydı oluşturulur.

## Raporlar

Mobil rapor ekranı:

- tarih aralığı / hazır dönemler,
- boş dönemde sıfır değerlerle güvenli sonuç,
- PDF görüntüleme,
- PDF paylaşma,
- Excel (`.xlsx`) paylaşma,
- CSV paylaşma

destekler.

CSV dosyası Excel'in Türkçe karakterleri doğru açması için UTF-8 BOM ve `;` ayırıcıyla yazılır.

## Mobil navigasyon

Alt navigasyon:

- Ana
- Araçlar
- Rapor (yetkisi olan kullanıcıda)
- Bildirim
- Daha

Hamburger / drawer:

- Onaylar & Davetler
- Ekip Yönetimi
- Denetim Kayıtları
- Görünüm & Arka Plan

menülerini kullanıcının yetkisine göre gösterir.

Android geri tuşu:

1. Drawer açıksa drawer'ı kapatır.
2. Alt sayfadaysa bir önceki sayfaya döner.
3. Ana kökteyse çıkış onayı gösterir.

## Görünüm

- Açık / koyu / sistem teması korunur.
- Vurgu renkleri korunur.
- Kartların sert gri çerçeveleri kaldırılmış, daha yumuşak gölge ve daha büyük radius kullanılmıştır.
- Kullanıcı cihazından özel arka plan resmi seçebilir; dosya uygulamanın yerel verisine kopyalanır.

## Güvenlik

- JWT `company_id` claim içerir.
- Token doğrulamasında kullanıcının veritabanındaki `CompanyId` ile claim eşleşmesi zorunludur.
- Şube erişimleri `CompanyId` üzerinden tekrar doğrulanır.
- Yönetici sorguları, raporlar, audit ve araç operasyonları tenant sınırına alınmıştır.
- 2FA / Authenticator akışı mevcut Identity TOTP altyapısını kullanmaya devam eder.
- Güvenlik damgası değiştiğinde eski JWT oturumları reddedilir.

## E-posta koduyla giriş

Kod akışı uygulamada hazırdır; gerçek e-posta teslimi için production ortamında SMTP yapılandırması gerekir:

- `Email__Host`
- `Email__Port`
- `Email__Username`
- `Email__Password`
- `Email__FromAddress`
- TLS/SSL ayarları

SMTP parolaları GitHub'a commit edilmemelidir. Render Environment/Secret üzerinden verilmelidir.

## Android release

Mobil sürüm: `3.1.0` / Android application version `10`.

`.github/workflows/android-apk.yml` şu kontrolleri geçmeden release oluşturmaz:

1. v3.1 kaynak güvenlik kontrolleri
2. API Release build
3. API testleri
4. Android Release build
5. APK publish
6. launcher icon doğrulaması
7. `main` için production API `/api/status` sürümünün `3.1` olması
8. production auth / registration smoke testleri

Başarılı `main` çalışmasında GitHub Release içine `VALE.apk` eklenir.

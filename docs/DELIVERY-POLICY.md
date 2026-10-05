# VALEM teslimat düzeni — 30 Eylül 2026

Kaynak değişikliklerini her iş grubu için yerel `fix/` veya `feature/` dalında tut. Doğrulanan işi yerel commit ile kaydet; commit işlemi GitHub'a gönderim değildir. Pazartesi dışında kaynak dallarını, main dalını veya yeni kaynak commitlerini işaret eden etiketleri GitHub'a gönderme.

Her sürümde imzalı APK/AAB oluştur, test et ve GitHub Release varlıklarını yayımla. Pazartesi öncesindeki yayın etiketi GitHub'daki mevcut main commitini hedefler; yayın açıklamasında APK'nın gerçek yerel kaynak commitini ve kaynak gönderiminin beklediğini açıkça belirt. APK/AAB hashlerini de yayınla. Kaynak gönderildiğinde etiketin commitini değiştirme; açıklamaya ilgili PR bağlantısını ekle.

VDS'yi her doğrulanmış sürümde yerel Git bundle ile güncelle. Dağıtım öncesi veritabanı yedeği al; başarısız dağıtımı geri al. GitHub hesabı flagged durumunda VDS'nin GitHub üzerinden çekmesine güvenme.

Pazartesi hazır ve test edilmiş dalları toplu gönder, her iş grubu için PR aç. Yarım kalan veya doğrulanmamış değişiklikleri gönderme/merge etme. PR oluşturmak dal gönderimi gerektirdiği için GitHub PR yedekleri de pazartesi oluşturulur. Pazartesiye kadar kaynak yedeğini Git bundle olarak proje dışındaki yerel yedek klasöründe ve VDS'de tut.

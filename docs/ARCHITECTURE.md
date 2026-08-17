# VALE Mimarisi

## Bileşenler

1. **VALE.Client**
   - Windows şube bilgisayarlarında çalışır.
   - Kullanıcı bilgilerini API'ye gönderir ve JWT erişim belirteci alır.
   - Veritabanı parolasını bilmez.
   - Token yalnızca süreç belleğinde tutulur.

2. **VALE.Api**
   - İnternette erişilebilen, HTTPS arkasında çalışan merkezi servis.
   - Kimlik doğrulama, şube yetkisi, ücret hesabı ve durum geçişlerini uygular.
   - PostgreSQL'e tek yetkili uygulama katmanıdır.

3. **PostgreSQL**
   - Şubeler, kullanıcılar, müşteriler, araçlar, vale fişleri ve ödemeleri saklar.
   - İnternete doğrudan istemci erişimi verilmemelidir.

## Yetki modeli

| Rol | Yetki |
| --- | --- |
| Admin | Tüm şubeler; sistem yönetimi |
| Manager | Atandığı şubenin tüm operasyonları |
| Valet | Araç kabulü, park ve araç isteme akışı |
| Cashier | Araç kabulü ve ödeme/teslim işlemi |

Normal kullanıcıların JWT'sinde `branch_id` bulunur. API, istek içindeki şube numarası değiştirilse bile bu kullanıcıyı başka şubenin verisine geçirmez.

## İş akışı

```text
Received -> Parked -> Requested -> Delivered
    |          |           |
    +----------+-----------+-> Cancelled
```

`Delivered` durumuna yalnızca ödeme/çıkış endpoint'i üzerinden geçilir. Ücret, giriş ve çıkış arasındaki başlamış her saat için hesaplanır; en az bir saat ücretlendirilir.

## Veri büyümesi

Liste endpoint'leri sayfalıdır ve en fazla 100 kayıt döndürür. Şube, durum ve giriş zamanı birleşik indeksi aktif araç ekranını hızlandırır. Plaka normalleştirilerek tekil araç kaydı korunur.


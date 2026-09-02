# VALE API Özeti

Temel yol: `/api`

| Yöntem | Yol | Açıklama | Yetki |
| --- | --- | --- | --- |
| POST | `/auth/login` | E-posta/parola ile giriş | Anonim, hız sınırlı |
| POST | `/auth/email-code/request` | Parolasız giriş kodu iste | Anonim, hız sınırlı |
| POST | `/auth/email-code/verify` | E-posta koduyla giriş | Anonim, hız sınırlı |
| POST | `/auth/refresh` | Hatırlanan cihaz oturumunu döndür | Anonim, hız sınırlı |
| POST | `/registration/owner` | Sade firma sahibi kaydı | Anonim, hız sınırlı |
| POST | `/registration/staff` | Davet koduyla personel başvurusu | Anonim, hız sınırlı |
| GET | `/auth/me` | Geçerli kullanıcı | Giriş yapılmış |
| GET | `/branches` | Erişilebilen şubeler | Personel |
| POST | `/admin/branches` | Yeni şube oluştur | Admin |
| GET | `/admin/users` | Kullanıcıları listele | Admin |
| POST | `/admin/users` | Şubeye bağlı kullanıcı oluştur | Admin |
| GET | `/admin/users/{id}/branches` | Kullanıcının erişebildiği şube gruplarını listele | Kullanıcı yöneticisi |
| PUT | `/admin/users/{id}/branches` | Varsayılan şube ve çoklu şube grubu erişimini güncelle | Firma-geneli yönetici |
| GET | `/dashboard?branchId=` | Şube özeti | Personel |
| GET | `/tickets` | Aktif/geçmiş kayıt arama | Personel |
| POST | `/tickets` | Yeni araç kabulü | Admin/Manager/Valet/Cashier |
| PATCH | `/tickets/{id}/status` | Park/istek/iptal durumu | Admin/Manager/Valet |
| POST | `/tickets/{id}/checkout` | Ücret al ve aracı teslim et | Admin/Manager/Cashier |
| GET | `/health/ready` | Servis ve veritabanı hazırlığı | Anonim |
| GET | `/health/email` | E-posta taşıyıcısı doğrulaması | Anonim, hız sınırlı |

Geliştirme ortamında OpenAPI tanımı `/openapi/v1.json` adresinde yayınlanır.

## Hata biçimi

API hataları RFC uyumlu `ProblemDetails` nesnesi döndürür:

```json
{
  "type": "about:blank",
  "title": "Araç zaten içeride",
  "status": 409,
  "detail": "Bu plakaya ait açık bir vale kaydı bulunuyor."
}
```

# VALE Company v3

Bu sürüm; rol hiyerarşisi, kullanıcı bazlı profil/tercihler, iki adımlı doğrulama, e-posta koduyla giriş, audit kayıtları, şube izolasyonu ve yönetim araçlarını kapsar.

## Yetki modeli

Owner > Admin > OperationsManager/Manager > BranchManager > Supervisor > Cashier/Valet. Auditor salt-okunur denetim ve raporlama rolüdür.

Yetki yalnızca mobil arayüzde gizlenmez; API policy kontrolleriyle sunucuda uygulanır. Şube erişimi branch-scoped, Owner/Admin/OperationsManager için cross-branch çalışır. Mali işlem oluşmuş kayıtlar silinemez; düzeltme ve silme hareketleri audit kaydına yazılır.

## Tenant ve şube grupları

Her kullanıcı tek bir `Company` tenantına bağlıdır. `BranchId` varsayılan şubeyi, `UserBranchMembership` kayıtları ise kullanıcının ek şube gruplarını belirtir. Owner/Admin/OperationsManager/Manager rolleri yalnız kendi firmaları içinde firma-geneli erişir; diğer roller yalnız aktif üyelikleri üzerinden şube seçebilir. API hiçbir işlemde istemcinin firma kimliğine güvenmez ve yabancı tenant nesnelerini bulunamadı gibi yanıtlayarak nesne kimliği sızıntısını sınırlar.

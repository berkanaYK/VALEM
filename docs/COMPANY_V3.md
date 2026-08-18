# VALE Company v3

Bu sürüm; rol hiyerarşisi, kullanıcı bazlı profil/tercihler, iki adımlı doğrulama, e-posta koduyla giriş, audit kayıtları, şube izolasyonu ve yönetim araçlarını kapsar.

## Yetki modeli

Owner > Admin > OperationsManager/Manager > BranchManager > Supervisor > Cashier/Valet. Auditor salt-okunur denetim ve raporlama rolüdür.

Yetki yalnızca mobil arayüzde gizlenmez; API policy kontrolleriyle sunucuda uygulanır. Şube erişimi branch-scoped, Owner/Admin/OperationsManager için cross-branch çalışır. Mali işlem oluşmuş kayıtlar silinemez; düzeltme ve silme hareketleri audit kaydına yazılır.

using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ApplicationSettingsPage : ContentPage
{
    public ApplicationSettingsPage(ApiClient api, UserDto user)
    {
        Title = "Ayarlar"; UiKit.StylePage(this);
        var body = new VerticalStackLayout { Padding = 18, Spacing = 14 };
        body.Add(UiKit.Label("Ayarlar", 27, true));
        var mode = UiKit.Picker("Görünüm modu");
        mode.ItemsSource = new[] { "Sistem ayarını kullan", "Açık / Beyaz", "Koyu" };
        mode.SelectedIndex = (int)ThemeService.CurrentMode;
        mode.SelectedIndexChanged += (_, _) => ThemeService.Apply((ValeThemeMode)Math.Max(0, mode.SelectedIndex));
        var saveMode = UiKit.SecondaryButton("Görünümü Hesabıma Kaydet");
        saveMode.Clicked += async (_, _) =>
        {
            if (user.Email == "preview@vale.invalid") { await DisplayAlertAsync("Deneme görünümü", "Mod bu deneme oturumunda uygulanır. Kalıcı ayarlar için hesabınızı oluşturun.", "Tamam"); return; }
            try
            {
                saveMode.IsEnabled = false;
                var p = await api.GetAccountProfileAsync();
                await api.UpdateAccountProfileAsync(new(p.FullName, p.PhoneNumber, ((ValeThemeMode)Math.Max(0, mode.SelectedIndex)).ToString(), p.AccentTheme, p.ProfileColor, p.BackgroundTheme, p.BirthDate, p.City, p.About, p.ProfileFrame, p.HeaderBackgroundTheme));
                await DisplayAlertAsync("Görünüm kaydedildi", "Tercihiniz sonraki girişlerinizde de uygulanacak.", "Tamam");
            }
            catch (Exception ex) { await DisplayAlertAsync("Kaydedilemedi", UserMessages.For(ex), "Tamam"); }
            finally { saveMode.IsEnabled = true; }
        };
        body.Add(UiKit.Card(new VerticalStackLayout { Spacing = 8, Children = { UiKit.Label("Görünüm", 18, true), mode, UiKit.Label("Mod değişikliği bütün ekranlara hemen uygulanır. Sonraki girişlerinizde de kullanmak için hesabınıza kaydedin.", 12, false, true), saveMode } }));
        void Link(string title, Func<Page> page)
        {
            var button = UiKit.SecondaryButton(title);
            button.Clicked += async (_, _) => await Navigation.PushAsync(page());
            body.Add(button);
        }
        Link("Profil, E-posta ve Temalar", () => new CompanyProfilePage(api, user));
        Link("Parolayı Değiştir", () => new ChangePasswordPage(api));
        Link("İki Adımlı Doğrulama", () => new TwoFactorPage(api));
        if (CompanyAccess.CanManageUsers(user))
        {
            body.Add(UiKit.Label("Yetkilendirme", 19, true));
            body.Add(UiKit.Label("Personelin rolünü ve şube erişimini düzenleyin. Birden çok rolün izinleri birlikte uygulanır. Yönetim yetkisi verdiğiniz personel ekip ayarlarını da değiştirebilir.", 12, false, true));
            body.Add(UiKit.Label("Vale: araç kabul ve teslim • Kasiyer: tahsilat • Denetçi: rapor ve denetim • Şube yöneticisi: şube operasyonu ve ekip • Yönetici: firma yönetimi", 12));
            Link("Personel Erişimlerini Düzenle", () => new TeamManagementPage(api, user));
        }
        Content = new ScrollView { Content = body };
    }
}

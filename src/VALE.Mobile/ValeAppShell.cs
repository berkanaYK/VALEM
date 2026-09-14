using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ValeAppShell : Shell
{
    public ValeAppShell(ApiClient api, UserDto user)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Title = "VALE";

        Shell.SetTabBarBackgroundColor(this, ThemeService.Palette.Card);
        Shell.SetTabBarTitleColor(this, ThemeService.Palette.Accent);
        Shell.SetTabBarUnselectedColor(this, ThemeService.Palette.Secondary);
        Shell.SetNavBarHasShadow(this, false);

        var tabs = new TabBar();
        tabs.Items.Add(CreateTab("Ana", new CompanyDashboardPage(api, user)));
        tabs.Items.Add(CreateTab("Araçlar", new CompanyTicketsPage(api, user)));

        if (CompanyAccess.CanReport(user))
            tabs.Items.Add(CreateTab("Rapor", new ModernReportsPage(api)));

        if (CompanyAccess.CanManageUsers(user))
            tabs.Items.Add(CreateTab("Ekip", new TeamManagementPage(api, user)));

        tabs.Items.Add(CreateTab("Daha", new MoreHubPage(api, user)));
        Items.Add(tabs);
    }

    private static Tab CreateTab(string shortTitle, Page page)
    {
        var content = new ShellContent
        {
            Title = shortTitle,
            Content = page
        };

        var tab = new Tab { Title = shortTitle };
        tab.Items.Add(content);
        return tab;
    }
}

public sealed class MoreHubPage : ContentPage
{
    public MoreHubPage(ApiClient api, UserDto user)
    {
        Title = "Ayarlar";
        UiKit.StylePage(this);
        var content = new VerticalStackLayout { Padding = 18, Spacing = 14 };
        content.Add(UiKit.Label("Ayarlar", 27, true));
        content.Add(UiKit.Label("Profilinizi, güvenliğinizi ve görünüm tercihlerinizi yönetin.", 13, false, true));
        void Link(string title, string detail, Func<Page> page)
        {
            var button = UiKit.SecondaryButton(title);
            button.Clicked += async (_, _) => await Navigation.PushAsync(page());
            content.Add(UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { button, UiKit.Label(detail, 12, false, true) } }));
        }
        Link("Profilim ve Fotoğrafım", "Fotoğraf, telefon, doğum tarihi, şehir ve kişisel bilgiler.", () => new CompanyProfilePage(api, user));
        Link("Görünüm ve Resimli Temalar", "Arka plan görseli, renkler, açık veya koyu görünüm.", () => new CompanyProfilePage(api, user));
        Link("Sürüm ve Satın Alma", "Kalan ücretsiz araç hakkınız, Sınırsız paket ve satın alma geri yükleme.", () => new PremiumPage(api, user));
        var guide = UiKit.SecondaryButton("Uygulama Rehberi");
        guide.Clicked += async (_, _) => await GuidedTour.ShowMainAsync(this, user, true);
        content.Add(UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { guide, UiKit.Label("Ana bölümleri ve kullanım adımlarını tekrar anlatır.", 12, false, true) } }));
        Link("Hesap Güvenliği", "İki adımlı doğrulama ve kurtarma kodları.", () => new TwoFactorPage(api));
        Link("Parolayı Değiştir", "Mevcut parolanızı güncelleyin.", () => new ChangePasswordPage(api));
        if (CompanyAccess.CanAudit(user)) Link("Denetim Kayıtları", "Firmanızdaki önemli işlemleri inceleyin.", () => new AuditPage(api));
        if (CompanyAccess.HasAny(user, ["Owner", "Admin"])) Link("Bağlantı Ayarları", "Sunucu bağlantısını kontrol edin.", () => new ConnectionSettingsPage(api));
        var developer = UiKit.TextButton("Geliştirici");
        developer.Clicked += async (_, _) => await Navigation.PushAsync(new DeveloperSettingsPage(api));
        content.Add(developer);
        content.Add(UiKit.Label($"VALEM {AppInfo.Current.VersionString} • Android", 12, false, true));
        var logout = UiKit.TextButton("Oturumu Kapat");
        logout.TextColor = ThemeService.Palette.Danger;
        logout.Clicked += async (_, _) => { await api.LogoutAsync(); App.ShowLogin(); };
        content.Add(logout);
        Content = new ScrollView { Content = content };
    }
}

public sealed class ValeTwoFactorPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Label _status = UiKit.Label("Durum kontrol ediliyor…", 13, false, true);
    private readonly VerticalStackLayout _body = new() { Spacing = 10 };
    private bool _busy;

    public ValeTwoFactorPage(ApiClient api)
    {
        _api = api;
        Title = "İki Adımlı Doğrulama";
        UiKit.StylePage(this);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 28),
                Spacing = 14,
                Children =
                {
                    UiKit.Label("İki adımlı doğrulama", 27, true),
                    UiKit.Label("Telefonunuzdaki doğrulama uygulamasıyla hesabınızı ek bir güvenlik katmanıyla koruyun.", 12.5, false, true),
                    UiKit.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { _status, _body }
                    })
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        try
        {
            _busy = true;
            var state = await _api.GetTwoFactorStatusAsync();
            _body.Clear();
            _status.Text = state.Enabled
                ? $"Açık • {state.RecoveryCodesLeft} kurtarma kodu kaldı"
                : "Kapalı • Kurulum yapabilirsiniz";

            if (!state.Enabled)
            {
                var setup = UiKit.PrimaryButton("Kurulumu Başlat");
                setup.Clicked += async (_, _) => await SetupAsync();
                _body.Add(setup);
            }
            else
            {
                var recovery = UiKit.SecondaryButton("Kurtarma Kodlarını Yenile");
                recovery.Clicked += async (_, _) => await RecoveryAsync();
                _body.Add(recovery);

                var disable = UiKit.TextButton("İki Adımlı Doğrulamayı Kapat");
                disable.TextColor = ThemeService.Palette.Danger;
                disable.Clicked += async (_, _) => await DisableAsync();
                _body.Add(disable);
            }
        }
        catch (Exception ex)
        {
            _status.Text = FriendlySecurityError(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SetupAsync()
    {
        try
        {
            var setup = await _api.SetupTwoFactorAsync();
            _body.Clear();

            var key = UiKit.Label(setup.SharedKey, 16, true);
            key.LineBreakMode = LineBreakMode.CharacterWrap;

            var copy = UiKit.SecondaryButton("Kurulum Anahtarını Kopyala");
            copy.Clicked += async (_, _) =>
            {
                await Clipboard.Default.SetTextAsync(setup.SharedKey);
                await DisplayAlertAsync("Kopyalandı", "Kurulum anahtarı panoya kopyalandı.", "Tamam");
            };

            var code = UiKit.Entry("6 haneli doğrulama kodu", Keyboard.Numeric);
            code.MaxLength = 6;
            var enable = UiKit.PrimaryButton("Doğrula ve Etkinleştir");
            enable.Clicked += async (_, _) =>
            {
                var normalized = (code.Text ?? string.Empty).Trim();
                if (normalized.Length != 6 || !normalized.All(char.IsDigit))
                {
                    await DisplayAlertAsync("Kod geçersiz", "Doğrulama uygulamasındaki 6 haneli kodu girin.", "Tamam");
                    return;
                }

                try
                {
                    enable.IsEnabled = false;
                    var result = await _api.EnableTwoFactorAsync(normalized);
                    await ShowRecoveryAsync(result.RecoveryCodes);
                    await RefreshAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("2FA etkinleştirilemedi", FriendlySecurityError(ex), "Tamam");
                }
                finally
                {
                    enable.IsEnabled = true;
                }
            };

            _body.Add(UiKit.Label("1. Authenticator uygulamanızda yeni hesap ekleyin.", 12.5));
            _body.Add(UiKit.Label("2. Aşağıdaki anahtarı uygulamaya girin.", 12.5));
            _body.Add(key);
            _body.Add(copy);
            _body.Add(UiKit.Label("3. Uygulamanın ürettiği 6 haneli kodu doğrulayın.", 12.5));
            _body.Add(code);
            _body.Add(enable);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("2FA kurulumu başlatılamadı", FriendlySecurityError(ex), "Tamam");
        }
    }

    private async Task RecoveryAsync()
    {
        var code = await DisplayPromptAsync("Kurtarma kodları", "Authenticator uygulamanızdaki güncel 6 haneli kodu girin.", "Yenile", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            var result = await _api.RegenerateRecoveryCodesAsync(code);
            await ShowRecoveryAsync(result.RecoveryCodes);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Kurtarma kodları yenilenemedi", FriendlySecurityError(ex), "Tamam");
        }
    }

    private async Task DisableAsync()
    {
        var code = await DisplayPromptAsync("İki adımlı doğrulamayı kapat", "Authenticator uygulamanızdaki güncel 6 haneli kodu girin.", "Kapat", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            await _api.DisableTwoFactorAsync(code);
            await DisplayAlertAsync("Kapatıldı", "İki adımlı doğrulama hesabınızdan kaldırıldı.", "Tamam");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("2FA kapatılamadı", FriendlySecurityError(ex), "Tamam");
        }
    }

    private async Task ShowRecoveryAsync(IReadOnlyList<string> codes)
    {
        var text = string.Join(Environment.NewLine, codes);
        await Clipboard.Default.SetTextAsync(text);
        await DisplayAlertAsync(
            "Kurtarma kodlarınız",
            $"Bu kodları güvenli bir yerde saklayın. Her kod yalnızca bir kez kullanılabilir. Kodlar panoya da kopyalandı.\n\n{text}",
            "Tamam");
    }

    private static string FriendlySecurityError(Exception ex)
    {
        var message = UserMessages.For(ex) ?? string.Empty;
        if (message.Contains("Giriş bilgileri hatalı", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Oturum geçersiz", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("401", StringComparison.OrdinalIgnoreCase))
        {
            return "Oturumunuz güvenlik işlemi için doğrulanamadı. Oturumu kapatıp yeniden giriş yaptıktan sonra tekrar deneyin.";
        }

        if (message.Contains("onay", StringComparison.OrdinalIgnoreCase) || message.Contains("aktif değil", StringComparison.OrdinalIgnoreCase))
            return "Hesabınız güvenlik ayarlarını değiştirmek için aktif durumda değil. Yönetici onayını kontrol edin.";

        if (message.Contains("kod", StringComparison.OrdinalIgnoreCase))
            return "Doğrulama kodu kabul edilmedi. Authenticator uygulamasındaki güncel 6 haneli kodu tekrar girin.";

        return string.IsNullOrWhiteSpace(message)
            ? "Güvenlik işlemi şu anda tamamlanamadı. Tekrar deneyin."
            : message;
    }
}

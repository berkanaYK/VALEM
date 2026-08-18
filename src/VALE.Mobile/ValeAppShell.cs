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
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly Entry _name = UiKit.Entry("Ad soyad");
    private readonly Entry _phone = UiKit.Entry("Telefon", Keyboard.Telephone);
    private readonly Label _email = UiKit.Label("—", 13.5);
    private readonly Label _branch = UiKit.Label("—", 13.5);
    private readonly Label _roles = UiKit.Label("—", 12.5, false, true);
    private readonly Picker _theme = UiKit.Picker("Tema");
    private readonly Picker _accent = UiKit.Picker("Vurgu rengi");
    private readonly Label _connection = UiKit.Label("Sunucu durumu henüz kontrol edilmedi.", 11.5, false, true);
    private AccountProfileDto? _profile;
    private bool _loading;

    public MoreHubPage(ApiClient api, UserDto user)
    {
        _api = api;
        _user = user;
        Title = "Daha Fazla";
        UiKit.StylePage(this);

        _theme.ItemsSource = new[] { "Sistem", "Açık", "Koyu" };
        _accent.ItemsSource = new[] { "Mavi", "İndigo", "Zümrüt", "Turuncu" };
        _theme.SelectedIndex = ThemeService.CurrentMode == ValeThemeMode.Dark ? 2 : ThemeService.CurrentMode == ValeThemeMode.Light ? 1 : 0;
        _accent.SelectedIndex = (int)ThemeService.CurrentAccent;

        var saveProfile = UiKit.PrimaryButton("Profil Bilgilerini Kaydet");
        saveProfile.Clicked += async (_, _) => await SaveProfileAsync(saveProfile);

        var twoFactor = UiKit.SecondaryButton("İki Adımlı Doğrulama");
        twoFactor.Clicked += async (_, _) => await Navigation.PushAsync(new ValeTwoFactorPage(_api));

        var password = UiKit.SecondaryButton("Parolayı Değiştir");
        password.Clicked += async (_, _) => await Navigation.PushAsync(new ChangePasswordPage(_api));

        var applyTheme = UiKit.PrimaryButton("Görünümü Uygula");
        applyTheme.Clicked += async (_, _) => await ApplyThemeAsync(applyTheme);

        var testConnection = UiKit.SecondaryButton("Bağlantıyı Kontrol Et");
        testConnection.Clicked += async (_, _) => await TestConnectionAsync(testConnection);

        var operations = new VerticalStackLayout { Spacing = 9 };
        if (CompanyAccess.CanAudit(user))
        {
            var audit = UiKit.SecondaryButton("Denetim Kayıtları");
            audit.Clicked += async (_, _) => await Navigation.PushAsync(new AuditPage(_api));
            operations.Add(audit);
        }
        if (CompanyAccess.HasAny(user, ["Owner", "Admin"]))
        {
            var advancedConnection = UiKit.SecondaryButton("Gelişmiş Bağlantı Ayarları");
            advancedConnection.Clicked += async (_, _) => await Navigation.PushAsync(new ConnectionSettingsPage(_api));
            operations.Add(advancedConnection);
        }

        var logout = UiKit.TextButton("Oturumu Kapat");
        logout.TextColor = ThemeService.Palette.Danger;
        logout.Clicked += (_, _) =>
        {
            _api.Logout();
            App.ShowLogin();
        };

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(16, 18, 16, 28),
            Spacing = 14,
            Children =
            {
                UiKit.Label("Daha fazla", 27, true),
                UiKit.Label("Profil, güvenlik, görünüm ve uygulama ayarlarını buradan yönetin.", 12.5, false, true),
                UiKit.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        UiKit.Label("Profil", 17, true),
                        _name,
                        _phone,
                        Detail("E-posta", _email),
                        Detail("Şube", _branch),
                        Detail("Yetkiler", _roles),
                        saveProfile
                    }
                }),
                UiKit.Card(new VerticalStackLayout
                {
                    Spacing = 9,
                    Children =
                    {
                        UiKit.Label("Hesap güvenliği", 17, true),
                        UiKit.Label("İki adımlı doğrulama ve parola ayarlarınızı yönetin.", 11.5, false, true),
                        twoFactor,
                        password
                    }
                }),
                UiKit.Card(new VerticalStackLayout
                {
                    Spacing = 9,
                    Children =
                    {
                        UiKit.Label("Görünüm", 17, true),
                        _theme,
                        _accent,
                        applyTheme
                    }
                }),
                UiKit.Card(new VerticalStackLayout
                {
                    Spacing = 9,
                    Children =
                    {
                        UiKit.Label("Bağlantı", 17, true),
                        _connection,
                        testConnection,
                        operations
                    }
                }),
                UiKit.Card(new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        UiKit.Label("Uygulama", 17, true),
                        UiKit.Label($"VALE {AppInfo.Current.VersionString} • Android", 12.5, false, true),
                        UiKit.Label("Yetkiler sunucu tarafından kontrol edilir ve önemli hesap işlemleri denetim kaydına alınır.", 11, false, true)
                    }
                }),
                logout
            }
        };

        Content = new ScrollView { Content = content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loading) await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        try
        {
            _loading = true;
            _profile = await _api.GetAccountProfileAsync();
            _name.Text = _profile.FullName;
            _phone.Text = _profile.PhoneNumber;
            _email.Text = _profile.Email;
            _branch.Text = _profile.BranchName ?? "—";
            _roles.Text = CompanyAccess.RolesText(_profile.Roles);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Profil yüklenemedi", ex.Message, "Tamam");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveProfileAsync(Button button)
    {
        if (_profile is null) await LoadProfileAsync();
        if (_profile is null) return;

        try
        {
            button.IsEnabled = false;
            var updated = await _api.UpdateAccountProfileAsync(new UpdateAccountProfileRequest(
                (_name.Text ?? string.Empty).Trim(),
                NullIfEmpty(_phone.Text),
                _profile.PreferredTheme,
                _profile.AccentTheme,
                _profile.ProfileColor));
            _profile = updated;
            await DisplayAlertAsync("Kaydedildi", "Profil bilgileriniz güncellendi.", "Tamam");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Profil kaydedilemedi", ex.Message, "Tamam");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async Task ApplyThemeAsync(Button button)
    {
        var mode = _theme.SelectedIndex == 2 ? ValeThemeMode.Dark : _theme.SelectedIndex == 1 ? ValeThemeMode.Light : ValeThemeMode.System;
        var accent = (ValeAccent)Math.Clamp(_accent.SelectedIndex, 0, 3);
        ThemeService.Apply(mode, accent);

        if (_profile is null) await LoadProfileAsync();
        if (_profile is null) return;

        try
        {
            button.IsEnabled = false;
            _profile = await _api.UpdateAccountProfileAsync(new UpdateAccountProfileRequest(
                _profile.FullName,
                _profile.PhoneNumber,
                mode.ToString(),
                accent.ToString(),
                _profile.ProfileColor));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Görünüm kaydedilemedi", ex.Message, "Tamam");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async Task TestConnectionAsync(Button button)
    {
        try
        {
            button.IsEnabled = false;
            _connection.Text = "Sunucu kontrol ediliyor…";
            await _api.TestConnectionAsync();
            _connection.Text = "Bağlantı hazır • VALE sunucusuna erişiliyor.";
        }
        catch (Exception ex)
        {
            _connection.Text = "Bağlantı kurulamadı.";
            await DisplayAlertAsync("Bağlantı", ex.Message, "Tamam");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private static View Detail(string title, Label value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children = { UiKit.Label(title, 10.5, true, true), value }
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
        var message = ex.Message ?? string.Empty;
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

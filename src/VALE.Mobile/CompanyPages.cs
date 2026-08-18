using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public static class CompanyAccess
{
    public static readonly string[] OwnerRoles = ["Owner"];
    public static readonly string[] CompanyRoles = ["Owner", "Admin", "OperationsManager", "Manager"];
    public static readonly string[] UserManagementRoles = ["Owner", "Admin", "OperationsManager", "Manager", "BranchManager"];
    public static readonly string[] OperationRoles = ["Owner", "Admin", "OperationsManager", "Manager", "BranchManager", "Supervisor", "Valet"];
    public static readonly string[] FinanceRoles = ["Owner", "Admin", "OperationsManager", "Manager", "BranchManager", "Cashier"];
    public static readonly string[] ReportRoles = ["Owner", "Admin", "OperationsManager", "Manager", "BranchManager", "Auditor"];
    public static readonly string[] AuditRoles = ["Owner", "Admin", "OperationsManager", "Manager", "Auditor"];
    public static readonly string[] EditRoles = ["Owner", "Admin", "OperationsManager", "Manager", "BranchManager", "Supervisor"];
    public static readonly string[] DeleteRoles = ["Owner", "Admin", "OperationsManager", "Manager", "BranchManager"];

    private static readonly Dictionary<string, int> Rank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Owner"] = 100, ["Admin"] = 90, ["OperationsManager"] = 80, ["Manager"] = 80,
        ["BranchManager"] = 70, ["Supervisor"] = 60, ["Auditor"] = 50, ["Cashier"] = 40, ["Valet"] = 30
    };

    public static bool HasAny(UserDto user, IEnumerable<string> roles) => user.Roles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    public static bool CanOperate(UserDto user) => HasAny(user, OperationRoles);
    public static bool CanFinance(UserDto user) => HasAny(user, FinanceRoles);
    public static bool CanReport(UserDto user) => HasAny(user, ReportRoles);
    public static bool CanManageUsers(UserDto user) => HasAny(user, UserManagementRoles);
    public static bool CanAudit(UserDto user) => HasAny(user, AuditRoles);
    public static bool CanEdit(UserDto user) => HasAny(user, EditRoles);
    public static bool CanDelete(UserDto user) => HasAny(user, DeleteRoles);
    public static bool CanManageBranches(UserDto user) => HasAny(user, CompanyRoles);

    public static IReadOnlyList<string> AssignableRoles(UserDto user)
    {
        var actorRank = user.Roles.Select(r => Rank.TryGetValue(r, out var rank) ? rank : 0).DefaultIfEmpty(0).Max();
        if (user.Roles.Contains("Owner", StringComparer.OrdinalIgnoreCase)) return Rank.Keys.OrderByDescending(x => Rank[x]).ToList();
        return Rank.Where(x => x.Value < actorRank).OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }

    public static string RoleText(string role) => role switch
    {
        "Owner" => "Patron / Sistem Sahibi",
        "Admin" => "Yönetici",
        "OperationsManager" or "Manager" => "Operasyon Yöneticisi",
        "BranchManager" => "Şube Müdürü",
        "Supervisor" => "Süpervizör",
        "Cashier" => "Kasiyer",
        "Valet" => "Vale Personeli",
        "Auditor" => "Denetçi",
        _ => role
    };

    public static string RolesText(IEnumerable<string> roles) => string.Join(" • ", roles.Select(RoleText).Distinct());
}

public sealed class CompanyMainTabsPage : TabbedPage
{
    public CompanyMainTabsPage(ApiClient api, UserDto user)
    {
        Title = "VALE";
        SetDynamicResource(BarBackgroundColorProperty, "ValeCard");
        SetDynamicResource(BarTextColorProperty, "ValeText");
        SelectedTabColor = ThemeService.Palette.Accent;
        UnselectedTabColor = ThemeService.Palette.Secondary;

        Children.Add(Tab("Ana Sayfa", new CompanyDashboardPage(api, user)));
        Children.Add(Tab("Araçlar", new CompanyTicketsPage(api, user)));
        if (CompanyAccess.CanReport(user)) Children.Add(Tab("Raporlar", new ModernReportsPage(api)));
        if (CompanyAccess.CanManageUsers(user)) Children.Add(Tab("Ekip", new TeamManagementPage(api, user)));
        if (CompanyAccess.CanAudit(user)) Children.Add(Tab("Denetim", new AuditPage(api)));
        Children.Add(Tab("Profil", new CompanyProfilePage(api, user)));
        Children.Add(Tab("Ayarlar", new CompanySettingsPage(api, user)));
    }

    private static NavigationPage Tab(string title, Page page)
    {
        page.Title = title;
        var nav = new NavigationPage(page) { Title = title };
        nav.SetDynamicResource(NavigationPage.BarBackgroundColorProperty, "ValeCard");
        nav.SetDynamicResource(NavigationPage.BarTextColorProperty, "ValeText");
        return nav;
    }
}

public sealed class CompanyDashboardPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly Label _active;
    private readonly Label _waiting;
    private readonly Label _delivered;
    private readonly Label? _revenue;
    private readonly ObservableCollection<TicketSummaryDto> _recent = new();
    private bool _busy;

    public CompanyDashboardPage(ApiClient api, UserDto user)
    {
        _api = api; _user = user; UiKit.StylePage(this);
        var active = UiKit.Metric("İÇERİDE", "—", "Aktif araç"); _active = active.Value;
        var waiting = UiKit.Metric("İSTENEN", "—", "Teslim bekliyor"); _waiting = waiting.Value;
        var delivered = UiKit.Metric("TESLİM", "—", "Bugün tamamlanan"); _delivered = delivered.Value;
        var cards = new List<View> { active.Card, waiting.Card, delivered.Card };
        if (CompanyAccess.CanFinance(user) || CompanyAccess.CanReport(user))
        {
            var revenue = UiKit.Metric("CİRO", "—", "Bugünkü tahsilat");
            _revenue = revenue.Value; cards.Add(revenue.Card);
        }

        var metrics = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10, RowSpacing = 10 };
        for (var i = 0; i < cards.Count; i++) metrics.Add(cards[i], i % 2, i / 2);

        var actions = new VerticalStackLayout { Spacing = 9 };
        if (CompanyAccess.CanOperate(user))
        {
            var add = UiKit.PrimaryButton("Yeni Araç Kabulü");
            add.Clicked += async (_, _) => await Navigation.PushAsync(new ModernNewTicketPage(_api));
            actions.Add(add);
        }
        if (CompanyAccess.CanReport(user))
        {
            var report = UiKit.SecondaryButton("Raporları Aç");
            report.Clicked += async (_, _) => await Navigation.PushAsync(new ModernReportsPage(_api));
            actions.Add(report);
        }
        if (CompanyAccess.CanManageUsers(user))
        {
            var team = UiKit.SecondaryButton("Ekip Yönetimi");
            team.Clicked += async (_, _) => await Navigation.PushAsync(new TeamManagementPage(_api, _user));
            actions.Add(team);
        }

        var list = new CollectionView { ItemsSource = _recent, SelectionMode = SelectionMode.Single, ItemTemplate = ModernTicketTemplates.Card(), HeightRequest = 330, EmptyView = UiKit.Label("Şu anda açık araç kaydı yok.", 13, false, true) };
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is TicketSummaryDto ticket)
            {
                list.SelectedItem = null;
                await Navigation.PushAsync(new CompanyTicketDetailPage(_api, _user, ticket.Id));
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 16,
                Children =
                {
                    UiKit.Label($"Merhaba, {FirstName(user.FullName)}", 27, true),
                    UiKit.Label($"{user.BranchName ?? "Şube atanmamış"} • {CompanyAccess.RolesText(user.Roles)}", 12.5, false, true),
                    metrics, actions, UiKit.Label("Son araçlar", 19, true), list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_busy) return;
        try
        {
            _busy = true;
            var data = await _api.GetDashboardAsync();
            _active.Text = data.ActiveVehicles.ToString(CultureInfo.CurrentCulture);
            _waiting.Text = data.WaitingForDelivery.ToString(CultureInfo.CurrentCulture);
            _delivered.Text = data.DeliveredToday.ToString(CultureInfo.CurrentCulture);
            if (_revenue is not null) _revenue.Text = $"{data.RevenueToday:N0} TL";
            _recent.Clear(); foreach (var item in data.RecentTickets) _recent.Add(item);
        }
        catch (Exception ex) { await DisplayAlertAsync("Ana sayfa", ex.Message, "Tamam"); }
        finally { _busy = false; }
    }

    private static string FirstName(string fullName) => fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
}

public sealed class AdvancedRegisterPage : ContentPage
{
    public AdvancedRegisterPage(ApiClient api)
    {
        Title = "Hesap Oluştur"; UiKit.StylePage(this);
        var name = UiKit.Entry("Ad soyad");
        var email = UiKit.Entry("Kurumsal e-posta", Keyboard.Email);
        var phone = UiKit.Entry("Telefon (isteğe bağlı)", Keyboard.Telephone);
        var employee = UiKit.Entry("Personel kodu (isteğe bağlı)");
        var branch = UiKit.Entry("Şube kodu");
        var password = UiKit.Entry("Parola", password: true);
        var repeat = UiKit.Entry("Parolayı tekrar girin", password: true);
        var save = UiKit.PrimaryButton("Hesabı Oluştur");

        save.Clicked += async (_, _) =>
        {
            if (password.Text != repeat.Text) { await DisplayAlertAsync("Parola", "Parolalar aynı değil.", "Tamam"); return; }
            try
            {
                save.IsEnabled = false;
                await api.EnsureServerReadyAsync();
                var result = await api.RegisterAsync(new RegisterRequest(name.Text ?? "", email.Text ?? "", password.Text ?? "", N(phone.Text), N(branch.Text), N(employee.Text)));
                await DisplayAlertAsync("Başvuru alındı", result.Message, "Tamam");
                await Navigation.PopAsync();
            }
            catch (Exception ex) { await DisplayAlertAsync("Hesap oluşturulamadı", ex.Message, "Tamam"); }
            finally { save.IsEnabled = true; }
        };

        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 14, Children = {
            UiKit.Label("VALE hesabınızı oluşturun", 27, true),
            UiKit.Label("Şube kodunuzu yöneticinizden öğrenebilirsiniz. Yeni hesaplar onaylandıktan sonra açılır.", 12.5, false, true),
            UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { name, email, phone, employee, branch, password, repeat, UiKit.Label("Parola en az 10 karakter olmalı; büyük/küçük harf, rakam ve özel karakter içermeli.", 11, false, true), save } })
        } } };
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class EmailCodeLoginPage : ContentPage
{
    public EmailCodeLoginPage(ApiClient api)
    {
        Title = "E-posta Koduyla Giriş"; UiKit.StylePage(this);
        var email = UiKit.Entry("E-posta adresiniz", Keyboard.Email);
        var code = UiKit.Entry("6 haneli kod", Keyboard.Numeric); code.IsVisible = false;
        var send = UiKit.PrimaryButton("Giriş Kodunu Gönder");
        var verify = UiKit.PrimaryButton("Giriş Yap"); verify.IsVisible = false;
        var info = UiKit.Label("Parola kullanmak istemiyorsanız e-posta adresinize tek kullanımlık giriş kodu gönderebilirsiniz.", 12.5, false, true);

        send.Clicked += async (_, _) =>
        {
            try
            {
                send.IsEnabled = false;
                await api.EnsureServerReadyAsync();
                await api.RequestEmailLoginCodeAsync(email.Text ?? "");
                code.IsVisible = verify.IsVisible = true;
                info.Text = "Kod gönderildi. E-postanızdaki 6 haneli kodu girin.";
            }
            catch (Exception ex) { await DisplayAlertAsync("Kod gönderilemedi", ex.Message, "Tamam"); }
            finally { send.IsEnabled = true; }
        };

        verify.Clicked += async (_, _) =>
        {
            try
            {
                verify.IsEnabled = false;
                LoginResponse login;
                try { login = await api.VerifyEmailLoginCodeAsync(email.Text ?? "", code.Text ?? ""); }
                catch (TwoFactorRequiredException)
                {
                    var totp = await DisplayPromptAsync("İki adımlı doğrulama", "Authenticator uygulamanızdaki 6 haneli kodu girin.", "Devam Et", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
                    if (string.IsNullOrWhiteSpace(totp)) return;
                    login = await api.VerifyEmailLoginCodeAsync(email.Text ?? "", code.Text ?? "", totp);
                }
                App.ShowAuthenticated(api, login.User);
            }
            catch (Exception ex) { await DisplayAlertAsync("Giriş yapılamadı", ex.Message, "Tamam"); }
            finally { verify.IsEnabled = true; }
        };

        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 14, Children = { UiKit.Label("E-posta koduyla giriş", 27, true), info, UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { email, send, code, verify } }) } } };
    }
}

public sealed class CompanyProfilePage : ContentPage
{
    private readonly ApiClient _api;
    private UserDto _user;
    private readonly Entry _name = UiKit.Entry("Ad soyad");
    private readonly Entry _phone = UiKit.Entry("Telefon", Keyboard.Telephone);
    private readonly Label _email = UiKit.Label("—", 13.5);
    private readonly Label _employee = UiKit.Label("—", 13.5);
    private readonly Label _job = UiKit.Label("—", 13.5);
    private readonly Label _branch = UiKit.Label("—", 13.5);
    private readonly Label _roles = UiKit.Label("—", 12.5, false, true);
    private readonly Label _avatar = UiKit.Label("VA", 26, true);
    private readonly Picker _theme = UiKit.Picker("Tema");
    private readonly Picker _accent = UiKit.Picker("Vurgu rengi");
    private readonly Picker _profileColor = UiKit.Picker("Profil rengi");
    private AccountProfileDto? _profile;

    public CompanyProfilePage(ApiClient api, UserDto user)
    {
        _api = api; _user = user; UiKit.StylePage(this);
        _theme.ItemsSource = new[] { "Sistem", "Açık", "Koyu" };
        _accent.ItemsSource = new[] { "Mavi", "İndigo", "Zümrüt", "Turuncu" };
        _profileColor.ItemsSource = new[] { "Mavi", "İndigo", "Zümrüt", "Turuncu", "Kırmızı", "Mor" };

        var avatarBox = new Border { StrokeThickness = 0, BackgroundColor = ThemeService.Palette.Accent, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 42 }, WidthRequest = 84, HeightRequest = 84, HorizontalOptions = LayoutOptions.Center, Content = _avatar };
        _avatar.HorizontalTextAlignment = TextAlignment.Center; _avatar.VerticalTextAlignment = TextAlignment.Center; _avatar.TextColor = Colors.White;

        var save = UiKit.PrimaryButton("Değişiklikleri Kaydet"); save.Clicked += async (_, _) => await SaveAsync(save, avatarBox);
        var security = UiKit.SecondaryButton("İki Adımlı Doğrulama"); security.Clicked += async (_, _) => await Navigation.PushAsync(new TwoFactorPage(_api));
        var password = UiKit.SecondaryButton("Parolayı Değiştir"); password.Clicked += async (_, _) => await Navigation.PushAsync(new ChangePasswordPage(_api));
        var logout = UiKit.TextButton("Oturumu Kapat"); logout.TextColor = ThemeService.Palette.Danger; logout.Clicked += (_, _) => { _api.Logout(); App.ShowLogin(); };

        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children = {
            avatarBox, UiKit.Label("Profilim", 27, true),
            UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { UiKit.Label("Kişisel bilgiler", 16, true), _name, _phone, Detail("E-posta", _email), Detail("Personel kodu", _employee), Detail("Görev", _job), Detail("Şube", _branch), Detail("Yetkiler", _roles) } }),
            UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { UiKit.Label("Görünüm", 16, true), _theme, _accent, _profileColor, UiKit.Label("Tema ve görünüm tercihleriniz hesabınıza kaydedilir.", 11, false, true) } }),
            save, security, password, logout
        } } };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }

    private async Task LoadAsync()
    {
        try
        {
            _profile = await _api.GetAccountProfileAsync();
            _name.Text = _profile.FullName; _phone.Text = _profile.PhoneNumber;
            _email.Text = _profile.Email; _employee.Text = _profile.EmployeeCode ?? "—"; _job.Text = _profile.JobTitle ?? "—"; _branch.Text = _profile.BranchName ?? "—";
            _roles.Text = CompanyAccess.RolesText(_profile.Roles); _avatar.Text = Initials(_profile.FullName);
            _theme.SelectedIndex = ThemeIndex(_profile.PreferredTheme); _accent.SelectedIndex = AccentIndex(_profile.AccentTheme); _profileColor.SelectedIndex = ProfileColorIndex(_profile.ProfileColor);
        }
        catch (Exception ex) { await DisplayAlertAsync("Profil", ex.Message, "Tamam"); }
    }

    private async Task SaveAsync(Button button, Border avatarBox)
    {
        try
        {
            button.IsEnabled = false;
            var request = new UpdateAccountProfileRequest(_name.Text ?? "", N(_phone.Text), ThemeValue(_theme.SelectedIndex), AccentValue(_accent.SelectedIndex), ProfileColorValue(_profileColor.SelectedIndex));
            _profile = await _api.UpdateAccountProfileAsync(request);
            ThemeService.ApplyServerPreferences(_profile.PreferredTheme, _profile.AccentTheme);
            avatarBox.BackgroundColor = Color.FromArgb(_profile.ProfileColor); _avatar.Text = Initials(_profile.FullName);
            await DisplayAlertAsync("Kaydedildi", "Profil ve görünüm tercihleriniz güncellendi.", "Tamam");
        }
        catch (Exception ex) { await DisplayAlertAsync("Profil kaydedilemedi", ex.Message, "Tamam"); }
        finally { button.IsEnabled = true; }
    }

    private static View Detail(string title, Label value) => new VerticalStackLayout { Spacing = 2, Children = { UiKit.Label(title, 10.5, true, true), value } };
    private static string Initials(string value) => string.Concat(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int ThemeIndex(string value) => value.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? 2 : value.Equals("Light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    private static string ThemeValue(int index) => index == 2 ? "Dark" : index == 1 ? "Light" : "System";
    private static int AccentIndex(string value) => value.Equals("Indigo", StringComparison.OrdinalIgnoreCase) ? 1 : value.Equals("Emerald", StringComparison.OrdinalIgnoreCase) ? 2 : value.Equals("Orange", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
    private static string AccentValue(int index) => index == 1 ? "Indigo" : index == 2 ? "Emerald" : index == 3 ? "Orange" : "Blue";
    private static readonly string[] ProfileColors = ["#2563EB", "#4F46E5", "#059669", "#EA580C", "#DC2626", "#9333EA"];
    private static int ProfileColorIndex(string value) { var i = Array.FindIndex(ProfileColors, x => x.Equals(value, StringComparison.OrdinalIgnoreCase)); return i < 0 ? 0 : i; }
    private static string ProfileColorValue(int index) => ProfileColors[Math.Clamp(index, 0, ProfileColors.Length - 1)];
}

public sealed class TwoFactorPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Label _status = UiKit.Label("Durum kontrol ediliyor…", 13, false, true);
    private readonly VerticalStackLayout _body = new() { Spacing = 10 };

    public TwoFactorPage(ApiClient api)
    {
        _api = api; Title = "İki Adımlı Doğrulama"; UiKit.StylePage(this);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children = { UiKit.Label("Hesap güvenliği", 27, true), UiKit.Label("Authenticator uygulamasıyla 6 haneli doğrulama kodu kullanabilirsiniz.", 12.5, false, true), UiKit.Card(new VerticalStackLayout { Spacing = 10, Children = { _status, _body } }) } } };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        try
        {
            var state = await _api.GetTwoFactorStatusAsync(); _body.Clear();
            _status.Text = state.Enabled ? $"Açık • {state.RecoveryCodesLeft} kurtarma kodu kaldı" : "Kapalı";
            if (!state.Enabled)
            {
                var setup = UiKit.PrimaryButton("2FA Kurulumunu Başlat"); setup.Clicked += async (_, _) => await SetupAsync(); _body.Add(setup);
            }
            else
            {
                var recovery = UiKit.SecondaryButton("Kurtarma Kodlarını Yenile"); recovery.Clicked += async (_, _) => await RecoveryAsync(); _body.Add(recovery);
                var disable = UiKit.TextButton("2FA'yı Kapat"); disable.TextColor = ThemeService.Palette.Danger; disable.Clicked += async (_, _) => await DisableAsync(); _body.Add(disable);
            }
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private async Task SetupAsync()
    {
        try
        {
            var setup = await _api.SetupTwoFactorAsync(); _body.Clear();
            var key = UiKit.Label(setup.SharedKey, 16, true); key.LineBreakMode = LineBreakMode.CharacterWrap;
            var copy = UiKit.SecondaryButton("Kurulum Anahtarını Kopyala"); copy.Clicked += async (_, _) => await Clipboard.Default.SetTextAsync(setup.SharedKey);
            var code = UiKit.Entry("Authenticator'daki 6 haneli kod", Keyboard.Numeric);
            var enable = UiKit.PrimaryButton("Doğrula ve Etkinleştir");
            enable.Clicked += async (_, _) =>
            {
                try
                {
                    var result = await _api.EnableTwoFactorAsync(code.Text ?? "");
                    await ShowRecoveryAsync(result.RecoveryCodes);
                    await RefreshAsync();
                }
                catch (Exception ex) { await DisplayAlertAsync("2FA açılamadı", ex.Message, "Tamam"); }
            };
            _body.Add(UiKit.Label("1. Authenticator uygulamanızda yeni hesap ekleyin.", 12.5)); _body.Add(UiKit.Label("2. Aşağıdaki anahtarı girin:", 12.5)); _body.Add(key); _body.Add(copy); _body.Add(code); _body.Add(enable);
        }
        catch (Exception ex) { await DisplayAlertAsync("2FA kurulumu", ex.Message, "Tamam"); }
    }

    private async Task RecoveryAsync()
    {
        var code = await DisplayPromptAsync("Kurtarma kodları", "Authenticator kodunuzu girin.", "Yenile", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
        if (string.IsNullOrWhiteSpace(code)) return;
        try { var result = await _api.RegenerateRecoveryCodesAsync(code); await ShowRecoveryAsync(result.RecoveryCodes); await RefreshAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("Kurtarma kodları", ex.Message, "Tamam"); }
    }

    private async Task DisableAsync()
    {
        var code = await DisplayPromptAsync("2FA'yı kapat", "Authenticator kodunuzu girin.", "Kapat", "Vazgeç", keyboard: Keyboard.Numeric, maxLength: 6);
        if (string.IsNullOrWhiteSpace(code)) return;
        try { await _api.DisableTwoFactorAsync(code); await RefreshAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("2FA kapatılamadı", ex.Message, "Tamam"); }
    }

    private async Task ShowRecoveryAsync(IReadOnlyList<string> codes)
    {
        var text = string.Join(Environment.NewLine, codes);
        await Clipboard.Default.SetTextAsync(text);
        await DisplayAlertAsync("Kurtarma kodlarınız", $"Bu kodları güvenli bir yerde saklayın. Her kod tek kullanımlıktır. Kodlar panoya da kopyalandı.\n\n{text}", "Tamam");
    }
}

public sealed class CompanySettingsPage : ContentPage
{
    public CompanySettingsPage(ApiClient api, UserDto user)
    {
        UiKit.StylePage(this);
        var theme = UiKit.Picker("Tema"); theme.ItemsSource = new[] { "Sistem", "Açık", "Koyu" }; theme.SelectedIndex = ThemeService.CurrentMode == ValeThemeMode.Dark ? 2 : ThemeService.CurrentMode == ValeThemeMode.Light ? 1 : 0;
        var accent = UiKit.Picker("Vurgu rengi"); accent.ItemsSource = new[] { "Mavi", "İndigo", "Zümrüt", "Turuncu" }; accent.SelectedIndex = (int)ThemeService.CurrentAccent;
        var apply = UiKit.PrimaryButton("Görünümü Uygula"); apply.Clicked += (_, _) => ThemeService.Apply(theme.SelectedIndex == 2 ? ValeThemeMode.Dark : theme.SelectedIndex == 1 ? ValeThemeMode.Light : ValeThemeMode.System, (ValeAccent)Math.Clamp(accent.SelectedIndex, 0, 3));
        var test = UiKit.SecondaryButton("Bağlantıyı Kontrol Et"); test.Clicked += async (_, _) => { try { test.IsEnabled = false; await api.TestConnectionAsync(); await DisplayAlertAsync("Bağlantı", "VALE sunucusu hazır.", "Tamam"); } catch (Exception ex) { await DisplayAlertAsync("Bağlantı", ex.Message, "Tamam"); } finally { test.IsEnabled = true; } };
        var content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children = { UiKit.Label("Ayarlar", 27, true), UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { UiKit.Label("Görünüm", 16, true), theme, accent, apply } }), UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { UiKit.Label("Bağlantı", 16, true), test } }) } };
        if (CompanyAccess.HasAny(user, ["Owner", "Admin"]))
        {
            var advanced = UiKit.SecondaryButton("Gelişmiş Bağlantı Ayarları"); advanced.Clicked += async (_, _) => await Navigation.PushAsync(new ConnectionSettingsPage(api)); ((VerticalStackLayout)((Border)content.Children[2]).Content!).Add(advanced);
        }
        content.Add(UiKit.Card(new VerticalStackLayout { Spacing = 4, Children = { UiKit.Label("Uygulama", 16, true), UiKit.Label($"VALE {AppInfo.Current.VersionString} • Android", 12.5, false, true), UiKit.Label("Yetkileriniz sunucu tarafından kontrol edilir. Hesabınızla ilgili işlemler denetim kaydına alınır.", 11, false, true) } }));
        Content = new ScrollView { Content = content };
    }
}

public sealed class CompanyTicketsPage : ContentPage
{
    private readonly ApiClient _api; private readonly UserDto _user; private readonly ObservableCollection<TicketSummaryDto> _items = new();
    private readonly Entry _search = UiKit.Entry("Plaka, fiş veya telefon ara"); private readonly Switch _closed = new() { OnColor = ThemeService.Palette.Accent }; private readonly CollectionView _list; private bool _busy;

    public CompanyTicketsPage(ApiClient api, UserDto user)
    {
        _api = api; _user = user; UiKit.StylePage(this);
        _list = new CollectionView { ItemsSource = _items, SelectionMode = SelectionMode.Single, ItemTemplate = ModernTicketTemplates.Card(), EmptyView = UiKit.Label("Kayıt bulunamadı.", 13, false, true) };
        _list.SelectionChanged += async (_, e) => { if (e.CurrentSelection.FirstOrDefault() is TicketSummaryDto ticket) { _list.SelectedItem = null; await Navigation.PushAsync(new CompanyTicketDetailPage(_api, _user, ticket.Id)); } };
        _search.Completed += async (_, _) => await RefreshAsync(); _closed.Toggled += async (_, _) => await RefreshAsync();
        var find = UiKit.SecondaryButton("Ara"); find.Clicked += async (_, _) => await RefreshAsync();
        var rows = new VerticalStackLayout { Spacing = 10 };
        var searchRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 }; searchRow.Add(_search, 0, 0); searchRow.Add(find, 1, 0);
        rows.Add(searchRow); rows.Add(new HorizontalStackLayout { Spacing = 9, Children = { _closed, UiKit.Label("Kapanan kayıtları göster", 12, false, true) } });
        if (CompanyAccess.CanOperate(user)) { var add = UiKit.PrimaryButton("Yeni Araç Kabulü"); add.Clicked += async (_, _) => await Navigation.PushAsync(new ModernNewTicketPage(_api)); rows.Add(add); }
        Content = new Grid { Padding = 16, RowSpacing = 10, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        var root = (Grid)Content; root.Add(UiKit.Label("Araçlar", 27, true), 0, 0); root.Add(rows, 0, 1); root.Add(_list, 0, 2);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }
    private async Task RefreshAsync() { if (_busy) return; try { _busy = true; var page = await _api.GetTicketsAsync(_search.Text, _closed.IsToggled); _items.Clear(); foreach (var item in page.Items) _items.Add(item); } catch (Exception ex) { await DisplayAlertAsync("Araçlar", ex.Message, "Tamam"); } finally { _busy = false; } }
}

public sealed class CompanyTicketDetailPage : ContentPage
{
    private readonly ApiClient _api; private readonly UserDto _user; private readonly Guid _id; private readonly VerticalStackLayout _body = new() { Spacing = 12 }; private readonly VerticalStackLayout _actions = new() { Spacing = 9 }; private bool _busy;
    public CompanyTicketDetailPage(ApiClient api, UserDto user, Guid id) { _api = api; _user = user; _id = id; Title = "Araç Detayı"; UiKit.StylePage(this); Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 14, Children = { _body, _actions } } }; }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync() { if (_busy) return; try { _busy = true; Render(await _api.GetTicketDetailAsync(_id)); } catch (Exception ex) { await DisplayAlertAsync("Araç detayı", ex.Message, "Tamam"); } finally { _busy = false; } }

    private void Render(TicketDetailDto detail)
    {
        var t = detail.Ticket; _body.Clear(); _actions.Clear(); _body.Add(UiKit.Label(t.LicensePlate, 30, true)); _body.Add(UiKit.Label(StatusText(t.Status), 13, true, true));
        if (!string.IsNullOrWhiteSpace(detail.PhotoBase64)) { try { var bytes = Convert.FromBase64String(detail.PhotoBase64); _body.Add(UiKit.Card(new Image { Source = ImageSource.FromStream(() => new MemoryStream(bytes)), HeightRequest = 230, Aspect = Aspect.AspectFill }, new Thickness(0), 20)); } catch { } }
        var vehicle = new VerticalStackLayout { Spacing = 7 }; vehicle.Add(UiKit.Label("Araç bilgileri", 17, true)); AddDetail(vehicle, "Marka / model", t.VehicleDescription); AddDetail(vehicle, "Model yılı", t.Year?.ToString() ?? "—"); AddDetail(vehicle, "Yakıt", t.FuelType ?? "—"); AddDetail(vehicle, "Şanzıman", t.Transmission ?? "—"); AddDetail(vehicle, "Park yeri", t.ParkingSpot ?? "—"); AddDetail(vehicle, "Anahtar", t.KeyTag ?? "—"); _body.Add(UiKit.Card(vehicle));
        var op = new VerticalStackLayout { Spacing = 7 }; op.Add(UiKit.Label("İşlem bilgileri", 17, true)); AddDetail(op, "Fiş", t.TicketNumber); AddDetail(op, "Giriş", t.EntryAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")); AddDetail(op, "Müşteri", t.CustomerName ?? "—"); AddDetail(op, "Telefon", t.CustomerPhone ?? "—"); if (CompanyAccess.CanFinance(_user) || CompanyAccess.CanReport(_user)) AddDetail(op, "Tutar", $"{t.AmountDue:N2} TL"); if (!string.IsNullOrWhiteSpace(t.Notes)) AddDetail(op, "Not", t.Notes); _body.Add(UiKit.Card(op));

        if (CompanyAccess.CanOperate(_user))
        {
            if (t.Status == TicketStatus.Received) AddAction("Park Edildi", () => ChangeStatus(TicketStatus.Parked));
            if (t.Status == TicketStatus.Parked) AddAction("Araç İsteniyor", () => ChangeStatus(TicketStatus.Requested));
            if (t.Status == TicketStatus.Requested) AddAction("Tekrar Parkta", () => ChangeStatus(TicketStatus.Parked));
        }
        if (CompanyAccess.CanFinance(_user) && t.Status == TicketStatus.Requested)
        {
            AddAction("Nakit ile Teslim Et", () => Checkout(PaymentMethod.Cash)); AddAction("Kart ile Teslim Et", () => Checkout(PaymentMethod.Card)); AddAction("Havale / EFT ile Teslim Et", () => Checkout(PaymentMethod.Transfer));
        }
        if (CompanyAccess.CanEdit(_user)) { var edit = UiKit.SecondaryButton("Kaydı Düzenle"); edit.Clicked += async (_, _) => await Navigation.PushAsync(new EditTicketPage(_api, _id)); _actions.Add(edit); }
        if (CompanyAccess.CanDelete(_user) && t.Status != TicketStatus.Delivered)
        {
            var delete = UiKit.TextButton("Kaydı Sil"); delete.TextColor = ThemeService.Palette.Danger; delete.Clicked += async (_, _) => await DeleteAsync(); _actions.Add(delete);
        }
    }

    private void AddAction(string text, Func<Task> action) { var button = UiKit.PrimaryButton(text); button.Clicked += async (_, _) => await action(); _actions.Add(button); }
    private async Task ChangeStatus(TicketStatus status) { try { await _api.UpdateStatusAsync(_id, status); await LoadAsync(); } catch (Exception ex) { await DisplayAlertAsync("İşlem tamamlanamadı", ex.Message, "Tamam"); } }
    private async Task Checkout(PaymentMethod method) { if (!await DisplayAlertAsync("Teslimi onayla", "Araç teslim edilip tahsilat kaydedilsin mi?", "Teslim Et", "Vazgeç")) return; try { var result = await _api.CheckoutAsync(_id, method); await DisplayAlertAsync("Teslim tamamlandı", $"Tahsilat: {result.PaidAmount:N2} TL", "Tamam"); await LoadAsync(); } catch (Exception ex) { await DisplayAlertAsync("Teslim tamamlanamadı", ex.Message, "Tamam"); } }
    private async Task DeleteAsync() { var reason = await DisplayPromptAsync("Kaydı sil", "Silme nedenini yazın. Bu işlem denetim kaydına alınır.", "Sil", "Vazgeç", maxLength: 300); if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3) return; if (!await DisplayAlertAsync("Son onay", "Bu açık kayıt listeden kaldırılacak. Devam edilsin mi?", "Evet, Sil", "Vazgeç")) return; try { await _api.DeleteTicketAsync(_id, reason); await DisplayAlertAsync("Kayıt silindi", "İşlem denetim kaydına eklendi.", "Tamam"); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlertAsync("Kayıt silinemedi", ex.Message, "Tamam"); } }
    private static void AddDetail(VerticalStackLayout layout, string name, string value) { layout.Add(UiKit.Label(name, 10.5, true, true)); layout.Add(UiKit.Label(value, 14)); }
    private static string StatusText(TicketStatus status) => status switch { TicketStatus.Received => "Teslim alındı", TicketStatus.Parked => "Parkta", TicketStatus.Requested => "Araç isteniyor", TicketStatus.Delivered => "Teslim edildi", TicketStatus.Cancelled => "İptal edildi", _ => status.ToString() };
}

public sealed class EditTicketPage : ContentPage
{
    private readonly ApiClient _api; private readonly Guid _id;
    private readonly Entry _plate = UiKit.Entry("Plaka"); private readonly Entry _brand = UiKit.Entry("Marka"); private readonly Entry _model = UiKit.Entry("Model"); private readonly Entry _color = UiKit.Entry("Renk"); private readonly Entry _year = UiKit.Entry("Model yılı", Keyboard.Numeric); private readonly Entry _fuel = UiKit.Entry("Yakıt"); private readonly Entry _transmission = UiKit.Entry("Şanzıman"); private readonly Entry _customer = UiKit.Entry("Müşteri adı"); private readonly Entry _phone = UiKit.Entry("Telefon", Keyboard.Telephone); private readonly Entry _key = UiKit.Entry("Anahtar etiketi"); private readonly Entry _spot = UiKit.Entry("Park yeri"); private readonly Editor _notes = UiKit.Editor("Notlar"); private readonly Entry _rate = UiKit.Entry("Saatlik ücret", Keyboard.Numeric); private readonly Switch _removePhoto = new();
    public EditTicketPage(ApiClient api, Guid id) { _api = api; _id = id; Title = "Kaydı Düzenle"; UiKit.StylePage(this); var save = UiKit.PrimaryButton("Değişiklikleri Kaydet"); save.Clicked += async (_, _) => await SaveAsync(save); Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 12, Children = { UiKit.Label("Araç kaydını düzenle", 27, true), UiKit.Label("Yaptığınız değişiklikler kullanıcı ve zaman bilgisiyle denetim kaydına eklenir.", 11.5, false, true), UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { _plate, _brand, _model, _color, _year, _fuel, _transmission, _customer, _phone, _key, _spot, _notes, _rate, new HorizontalStackLayout { Spacing = 9, Children = { _removePhoto, UiKit.Label("Mevcut araç fotoğrafını kaldır", 12.5) } }, save } }) } } }; }
    protected override async void OnAppearing() { base.OnAppearing(); try { var d = await _api.GetTicketDetailAsync(_id); var t = d.Ticket; _plate.Text = t.LicensePlate; var parts = t.VehicleDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries); _brand.Text = parts.ElementAtOrDefault(0); _model.Text = parts.ElementAtOrDefault(1); _year.Text = t.Year?.ToString(); _fuel.Text = t.FuelType; _transmission.Text = t.Transmission; _customer.Text = t.CustomerName; _phone.Text = t.CustomerPhone; _key.Text = t.KeyTag; _spot.Text = t.ParkingSpot; _notes.Text = t.Notes; _rate.Text = t.HourlyRate.ToString(CultureInfo.CurrentCulture); } catch (Exception ex) { await DisplayAlertAsync("Kayıt", ex.Message, "Tamam"); } }
    private async Task SaveAsync(Button save) { int? year = int.TryParse(_year.Text, out var y) ? y : null; decimal? rate = decimal.TryParse(_rate.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var r) ? r : null; try { save.IsEnabled = false; await _api.UpdateTicketDetailsAsync(_id, new UpdateTicketDetailsRequest(_plate.Text ?? "", N(_brand.Text), N(_model.Text), N(_color.Text), year, N(_fuel.Text), N(_transmission.Text), N(_customer.Text), N(_phone.Text), N(_key.Text), N(_spot.Text), N(_notes.Text), rate, null, _removePhoto.IsToggled)); await DisplayAlertAsync("Kaydedildi", "Araç kaydı güncellendi.", "Tamam"); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlertAsync("Kayıt güncellenemedi", ex.Message, "Tamam"); } finally { save.IsEnabled = true; } }
    private static string? N(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class TeamManagementPage : ContentPage
{
    private readonly ApiClient _api; private readonly UserDto _actor; private readonly ObservableCollection<AdminUserDto> _users = new(); private readonly CollectionView _list;
    public TeamManagementPage(ApiClient api, UserDto actor) { _api = api; _actor = actor; UiKit.StylePage(this); _list = new CollectionView { ItemsSource = _users, SelectionMode = SelectionMode.Single, EmptyView = UiKit.Label("Personel kaydı yok.", 13, false, true), ItemTemplate = new DataTemplate(() => { var name = UiKit.Label("", 15, true); name.SetBinding(Label.TextProperty, nameof(AdminUserDto.FullName)); var email = UiKit.Label("", 11.5, false, true); email.SetBinding(Label.TextProperty, nameof(AdminUserDto.Email)); var status = UiKit.Label("", 11.5, true, true); status.SetBinding(Label.TextProperty, nameof(AdminUserDto.StatusText)); return UiKit.Card(new VerticalStackLayout { Spacing = 2, Children = { name, email, status } }, new Thickness(12), 14); }) }; _list.SelectionChanged += async (_, e) => { if (e.CurrentSelection.FirstOrDefault() is AdminUserDto u) { _list.SelectedItem = null; await Navigation.PushAsync(new UserEditPage(_api, _actor, u.Id)); } }; var actions = new HorizontalStackLayout { Spacing = 8 }; var add = UiKit.PrimaryButton("Personel Ekle"); add.Clicked += async (_, _) => await Navigation.PushAsync(new CreateTeamUserPage(_api, _actor)); actions.Add(add); if (CompanyAccess.CanManageBranches(actor)) { var branch = UiKit.SecondaryButton("Şube Ekle"); branch.Clicked += async (_, _) => await Navigation.PushAsync(new CreateBranchPage(_api)); actions.Add(branch); } Content = new Grid { Padding = 16, RowSpacing = 10, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } }; var root = (Grid)Content; root.Add(UiKit.Label("Ekip yönetimi", 27, true), 0, 0); root.Add(actions, 0, 1); root.Add(_list, 0, 2); }
    protected override async void OnAppearing() { base.OnAppearing(); try { var users = await _api.GetAdminUsersAsync(); _users.Clear(); foreach (var u in users) _users.Add(u); } catch (Exception ex) { await DisplayAlertAsync("Ekip", ex.Message, "Tamam"); } }
}

public sealed class UserEditPage : ContentPage
{
    private readonly ApiClient _api; private readonly UserDto _actor; private readonly Guid _id; private readonly Entry _name = UiKit.Entry("Ad soyad"); private readonly Entry _phone = UiKit.Entry("Telefon", Keyboard.Telephone); private readonly Entry _employee = UiKit.Entry("Personel kodu"); private readonly Entry _job = UiKit.Entry("Görev / unvan"); private readonly Picker _branch = UiKit.Picker("Şube"); private readonly Switch _active = new() { OnColor = ThemeService.Palette.Accent }; private readonly VerticalStackLayout _roleBox = new() { Spacing = 6 }; private readonly Dictionary<string, Switch> _roles = new(StringComparer.OrdinalIgnoreCase); private List<BranchDto> _branches = [];
    public UserEditPage(ApiClient api, UserDto actor, Guid id) { _api = api; _actor = actor; _id = id; Title = "Personel Düzenle"; UiKit.StylePage(this); var save = UiKit.PrimaryButton("Personeli Kaydet"); save.Clicked += async (_, _) => await SaveAsync(save); Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 12, Children = { UiKit.Label("Personel bilgileri", 27, true), UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { _name, _phone, _employee, _job, _branch, new HorizontalStackLayout { Spacing = 9, Children = { _active, UiKit.Label("Hesap aktif", 12.5) } }, UiKit.Label("Yetkiler", 13, true), _roleBox, save } }) } } }; }
    protected override async void OnAppearing() { base.OnAppearing(); try { var detail = await _api.GetAdminUserAsync(_id); _branches = (await _api.GetBranchesAsync()).ToList(); _branch.ItemsSource = _branches.Select(x => $"{x.Code} • {x.Name}").ToList(); _branch.SelectedIndex = _branches.FindIndex(x => x.Id == detail.BranchId); _name.Text = detail.FullName; _phone.Text = detail.PhoneNumber; _employee.Text = detail.EmployeeCode; _job.Text = detail.JobTitle; _active.IsToggled = detail.IsActive; _roleBox.Clear(); _roles.Clear(); foreach (var role in CompanyAccess.AssignableRoles(_actor)) { var sw = new Switch { OnColor = ThemeService.Palette.Accent, IsToggled = detail.Roles.Contains(role, StringComparer.OrdinalIgnoreCase) }; _roles[role] = sw; _roleBox.Add(new HorizontalStackLayout { Spacing = 8, Children = { sw, UiKit.Label(CompanyAccess.RoleText(role), 12.5) } }); } } catch (Exception ex) { await DisplayAlertAsync("Personel", ex.Message, "Tamam"); } }
    private async Task SaveAsync(Button save) { if (_branch.SelectedIndex < 0 || _branch.SelectedIndex >= _branches.Count) { await DisplayAlertAsync("Şube", "Bir şube seçin.", "Tamam"); return; } var selected = _roles.Where(x => x.Value.IsToggled).Select(x => x.Key).ToList(); if (selected.Count == 0) { await DisplayAlertAsync("Yetki", "En az bir görev/yetki seçin.", "Tamam"); return; } try { save.IsEnabled = false; await _api.UpdateAdminUserAsync(_id, new UpdateAdminUserRequest(_name.Text ?? "", N(_phone.Text), N(_employee.Text), N(_job.Text), _branches[_branch.SelectedIndex].Id, _active.IsToggled, selected)); await DisplayAlertAsync("Kaydedildi", "Personel bilgileri ve yetkileri güncellendi.", "Tamam"); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlertAsync("Personel kaydedilemedi", ex.Message, "Tamam"); } finally { save.IsEnabled = true; } }
    private static string? N(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class CreateTeamUserPage : ContentPage
{
    private readonly ApiClient _api; private readonly UserDto _actor; private List<BranchDto> _branches = [];
    public CreateTeamUserPage(ApiClient api, UserDto actor) { _api = api; _actor = actor; Title = "Personel Ekle"; UiKit.StylePage(this); var name = UiKit.Entry("Ad soyad"); var email = UiKit.Entry("E-posta", Keyboard.Email); var password = UiKit.Entry("Geçici parola", password: true); var branch = UiKit.Picker("Şube"); var role = UiKit.Picker("Görev"); var assignable = CompanyAccess.AssignableRoles(actor).ToList(); role.ItemsSource = assignable.Select(CompanyAccess.RoleText).ToList(); var save = UiKit.PrimaryButton("Personel Hesabı Oluştur"); save.Clicked += async (_, _) => { if (branch.SelectedIndex < 0 || role.SelectedIndex < 0) { await DisplayAlertAsync("Eksik bilgi", "Şube ve görev seçin.", "Tamam"); return; } try { save.IsEnabled = false; await _api.CreateAdminUserAsync(new CreateUserRequest(email.Text ?? "", password.Text ?? "", name.Text ?? "", _branches[branch.SelectedIndex].Id, [assignable[role.SelectedIndex]])); await DisplayAlertAsync("Personel eklendi", "Hesap aktif olarak oluşturuldu.", "Tamam"); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlertAsync("Personel eklenemedi", ex.Message, "Tamam"); } finally { save.IsEnabled = true; } }; Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 12, Children = { UiKit.Label("Yeni personel", 27, true), UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { name, email, password, branch, role, UiKit.Label("Geçici parolayı kullanıcıya güvenli bir kanaldan iletin; ilk girişten sonra değiştirmesini isteyin.", 11, false, true), save } }) } } }; Loaded += async (_, _) => { try { _branches = (await _api.GetBranchesAsync()).ToList(); branch.ItemsSource = _branches.Select(x => $"{x.Code} • {x.Name}").ToList(); if (_branches.Count == 1) branch.SelectedIndex = 0; } catch (Exception ex) { await DisplayAlertAsync("Şubeler", ex.Message, "Tamam"); } }; }
}

public sealed class CreateBranchPage : ContentPage
{
    public CreateBranchPage(ApiClient api) { Title = "Şube Ekle"; UiKit.StylePage(this); var code = UiKit.Entry("Şube kodu"); var name = UiKit.Entry("Şube adı"); var city = UiKit.Entry("Şehir"); var address = UiKit.Entry("Adres"); var save = UiKit.PrimaryButton("Şubeyi Oluştur"); save.Clicked += async (_, _) => { try { save.IsEnabled = false; await api.CreateBranchAsync(new CreateBranchRequest(code.Text ?? "", name.Text ?? "", city.Text ?? "", string.IsNullOrWhiteSpace(address.Text) ? null : address.Text.Trim())); await DisplayAlertAsync("Şube oluşturuldu", "Yeni şube kullanıma hazır.", "Tamam"); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlertAsync("Şube oluşturulamadı", ex.Message, "Tamam"); } finally { save.IsEnabled = true; } }; Content = new VerticalStackLayout { Padding = 16, Spacing = 12, Children = { UiKit.Label("Yeni şube", 27, true), UiKit.Card(new VerticalStackLayout { Spacing = 9, Children = { code, name, city, address, save } }) } }; }
}

public sealed class AuditPage : ContentPage
{
    private readonly ApiClient _api; private readonly ObservableCollection<AuditEntryDto> _rows = new(); private readonly CollectionView _list;
    public AuditPage(ApiClient api) { _api = api; UiKit.StylePage(this); _list = new CollectionView { ItemsSource = _rows, SelectionMode = SelectionMode.None, EmptyView = UiKit.Label("Denetim kaydı bulunamadı.", 13, false, true), ItemTemplate = new DataTemplate(() => { var action = UiKit.Label("", 14, true); action.SetBinding(Label.TextProperty, nameof(AuditEntryDto.Action)); var who = UiKit.Label("", 11.5, false, true); who.SetBinding(Label.TextProperty, nameof(AuditEntryDto.UserName)); var detail = UiKit.Label("", 12, false, true); detail.SetBinding(Label.TextProperty, nameof(AuditEntryDto.Detail)); var time = UiKit.Label("", 10.5, false, true); time.SetBinding(Label.TextProperty, new Binding(nameof(AuditEntryDto.OccurredAt), stringFormat: "{0:dd.MM.yyyy HH:mm}")); return UiKit.Card(new VerticalStackLayout { Spacing = 3, Children = { action, who, detail, time } }, new Thickness(12), 14); }) }; Content = new Grid { Padding = 16, RowSpacing = 10, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } }; var root = (Grid)Content; root.Add(UiKit.Label("Denetim kayıtları", 27, true), 0, 0); root.Add(UiKit.Label("Girişler, kullanıcı değişiklikleri, araç hareketleri, düzeltme ve silme işlemleri burada izlenir.", 11.5, false, true), 0, 1); root.Add(_list, 0, 2); }
    protected override async void OnAppearing() { base.OnAppearing(); try { var rows = await _api.GetAuditAsync(); _rows.Clear(); foreach (var row in rows) _rows.Add(row); } catch (Exception ex) { await DisplayAlertAsync("Denetim", ex.Message, "Tamam"); } }
}

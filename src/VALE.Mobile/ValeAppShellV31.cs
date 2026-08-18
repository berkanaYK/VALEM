using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ValeAppShellV31 : Shell
{
    public ValeAppShellV31(ApiClient api, UserDto user)
    {
        FlyoutBehavior = FlyoutBehavior.Flyout;
        FlyoutWidth = 310;
        Title = "VALE";
        Shell.SetTabBarBackgroundColor(this, ThemeService.Palette.Card);
        Shell.SetTabBarTitleColor(this, ThemeService.Palette.Accent);
        Shell.SetTabBarUnselectedColor(this, ThemeService.Palette.Secondary);
        Shell.SetNavBarHasShadow(this, false);

        FlyoutHeader = new VerticalStackLayout
        {
            Padding = new Thickness(20, 30, 20, 18),
            Spacing = 4,
            Children =
            {
                UiKit.Label("VALE", 26, true),
                UiKit.Label(user.FullName, 13, true),
                UiKit.Label(user.BranchName ?? "Şube atanmamış", 11.5, false, true)
            }
        };

        var tabs = new TabBar { Title = "VALE" };
        tabs.Items.Add(CreateTab("🏠 Ana", new CompanyDashboardPage(api, user)));
        tabs.Items.Add(CreateTab("🚗 Araçlar", new CompanyTicketsPage(api, user)));
        tabs.Items.Add(CreateTab("📊 Raporlar", CompanyAccess.CanReport(user)
            ? new ReportsV31Page(api)
            : new RestrictedPage("Raporlar", "Bu hesap için rapor görüntüleme yetkisi tanımlı değil.")));
        tabs.Items.Add(CreateTab("🔔 Bildirim", new NotificationsPage(api, user)));
        tabs.Items.Add(CreateTab("☰ Daha", new MoreHubPage(api, user)));
        Items.Add(tabs);

        Items.Add(CreateFlyout("👤 Profil", new CompanyProfilePage(api, user)));
        if (CompanyAccess.CanManageUsers(user)) Items.Add(CreateFlyout("👥 Ekip", new TeamManagementPage(api, user)));
        if (CompanyAccess.CanManageBranches(user)) Items.Add(CreateFlyout("🏢 Firma & Şubeler", new CompanyManagementPage(api)));
        if (CompanyAccess.CanAudit(user)) Items.Add(CreateFlyout("🧾 Denetim Kayıtları", new AuditPage(api)));
        Items.Add(CreateFlyout("⚙️ Ayarlar", new MoreHubPage(api, user)));
        Items.Add(CreateFlyout("❓ Yardım", new ValeHelpPage()));
        Items.Add(CreateFlyout("🚪 Çıkış", new LogoutPage(api)));
    }

    private static Tab CreateTab(string title, Page page)
    {
        page.Title = title;
        var tab = new Tab { Title = title };
        tab.Items.Add(new ShellContent { Title = title, Content = page });
        return tab;
    }

    private static FlyoutItem CreateFlyout(string title, Page page)
    {
        page.Title = title;
        return new FlyoutItem
        {
            Title = title,
            Items = { new ShellContent { Title = title, Content = page } }
        };
    }
}

public sealed class RestrictedPage : ContentPage
{
    public RestrictedPage(string title, string message)
    {
        Title = title;
        UiKit.StylePage(this);
        Content = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children = { UiKit.Label(title, 26, true), UiKit.Label(message, 13, false, true) }
        };
    }
}

public sealed class CompanyManagementPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly VerticalStackLayout _list = new() { Spacing = 8 };
    private bool _busy;

    public CompanyManagementPage(ApiClient api)
    {
        _api = api;
        Title = "Firma & Şubeler";
        UiKit.StylePage(this);
        var add = UiKit.PrimaryButton("Yeni Şube Oluştur");
        add.Clicked += async (_, _) => await AddBranchAsync();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 18, 16, 28), Spacing = 14,
                Children =
                {
                    UiKit.Label("Firma ve şubeler", 27, true),
                    UiKit.Label("Şube kodları yalnızca kendi firmanız içinde benzersizdir. Personel, firma + şube koduyla veya davet koduyla katılabilir.", 12.5, false, true),
                    add,
                    _list
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
            _list.Clear();
            var branches = await _api.GetBranchesAsync();
            foreach (var branch in branches)
            {
                var stack = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        UiKit.Label(branch.Name, 16, true),
                        UiKit.Label($"Kod: {branch.Code} • {branch.City}", 12, false, true),
                        UiKit.Label(string.IsNullOrWhiteSpace(branch.Address) ? "Adres girilmemiş" : branch.Address, 11, false, true)
                    }
                };
                if (!string.IsNullOrWhiteSpace(branch.InviteCode))
                {
                    var invite = UiKit.Label($"Davet kodu: {branch.InviteCode}", 12, true);
                    stack.Add(invite);
                    var copy = UiKit.TextButton("Davet kodunu kopyala");
                    copy.Clicked += async (_, _) =>
                    {
                        await Clipboard.Default.SetTextAsync(branch.InviteCode);
                        await DisplayAlertAsync("Davet kodu", "Davet kodu panoya kopyalandı.", "Tamam");
                    };
                    stack.Add(copy);
                }
                _list.Add(UiKit.Card(stack));
            }
            if (branches.Count == 0) _list.Add(UiKit.Label("Aktif şube bulunamadı.", 13, false, true));
        }
        catch (Exception ex) { await DisplayAlertAsync("Şubeler", ex.Message, "Tamam"); }
        finally { _busy = false; }
    }

    private async Task AddBranchAsync()
    {
        var code = await DisplayPromptAsync("Yeni şube", "Şube kodu", "Devam", "Vazgeç", maxLength: 20);
        if (string.IsNullOrWhiteSpace(code)) return;
        var name = await DisplayPromptAsync("Yeni şube", "Şube adı", "Devam", "Vazgeç", maxLength: 120);
        if (string.IsNullOrWhiteSpace(name)) return;
        var city = await DisplayPromptAsync("Yeni şube", "Şehir", "Devam", "Vazgeç", maxLength: 80);
        if (string.IsNullOrWhiteSpace(city)) return;
        var address = await DisplayPromptAsync("Yeni şube", "Adres (isteğe bağlı)", "Oluştur", "Vazgeç", maxLength: 300);
        try
        {
            var created = await _api.CreateBranchAsync(new CreateBranchRequest(code.Trim(), name.Trim(), city.Trim(), string.IsNullOrWhiteSpace(address) ? null : address.Trim()));
            if (!string.IsNullOrWhiteSpace(created.InviteCode))
                await DisplayAlertAsync("Şube oluşturuldu", $"Personel davet kodu: {created.InviteCode}", "Tamam");
            await RefreshAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Şube oluşturulamadı", ex.Message, "Tamam"); }
    }
}

public sealed class ValeHelpPage : ContentPage
{
    public ValeHelpPage()
    {
        Title = "Yardım";
        UiKit.StylePage(this);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 22, 18, 30), Spacing = 12,
                Children =
                {
                    UiKit.Label("VALE Yardım", 27, true),
                    UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { UiKit.Label("Personel katılımı", 16, true), UiKit.Label("Firma yöneticinizden firma + şube kodunu veya davet kodunu alın. Başvurunuz yalnızca ilgili firmanın yöneticilerine düşer.", 12.5, false, true) } }),
                    UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { UiKit.Label("Araç ücreti", 16, true), UiKit.Label("Aktif aracın tahmini ücreti araç detayında anlık gösterilir; kesin tahsilat teslim anında hesaplanır.", 12.5, false, true) } }),
                    UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { UiKit.Label("Güvenlik", 16, true), UiKit.Label("Authenticator ile 2FA kullanabilirsiniz. E-posta koduyla giriş, sunucudaki SMTP ayarları etkinse kullanılabilir.", 12.5, false, true) } })
                }
            }
        };
    }
}

public sealed class LogoutPage : ContentPage
{
    private readonly ApiClient _api;
    private bool _done;
    public LogoutPage(ApiClient api)
    {
        _api = api;
        Title = "Çıkış";
        UiKit.StylePage(this);
        Content = UiKit.Label("Oturum kapatılıyor…", 14, false, true);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_done) return;
        _done = true;
        _api.Logout();
        App.ShowLogin();
    }
}

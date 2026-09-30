using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class ValeAppShellV31 : Shell
{
    private readonly Stack<ShellItem> _topLevelHistory = new();
    private ShellItem? _observedItem;
    private ShellItem? _homeItem;
    private bool _restoringHistory;
    private readonly Image _headerAvatar = new() { Aspect = Aspect.AspectFill, IsVisible = false };
    private readonly Label _headerInitials = UiKit.Label("VA", 20, true);
    private readonly Label _headerName = UiKit.Label("", 13, true);
    private bool _loadingAvatar;
    private readonly Image _headerBackground = new() { Aspect = Aspect.AspectFill };

    public ValeAppShellV31(ApiClient api, UserDto user)
    {
        FlyoutBehavior = FlyoutBehavior.Flyout;
        FlyoutWidth = 310;
        Title = "VALEM";
        SetDynamicResource(Shell.FlyoutBackgroundColorProperty, "ValePage");
        Shell.SetTabBarBackgroundColor(this, ThemeService.Palette.Card);
        Shell.SetTabBarTitleColor(this, ThemeService.Palette.Accent);
        Shell.SetTabBarUnselectedColor(this, ThemeService.Palette.Secondary);
        Shell.SetBackgroundColor(this, ThemeService.Palette.Card);
        Shell.SetTitleColor(this, ThemeService.Palette.Text);
        Shell.SetForegroundColor(this, ThemeService.Palette.Text);
        Shell.SetNavBarHasShadow(this, false);

        _headerInitials.Text = string.Concat(user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
        _headerName.Text = user.FullName;
        _headerName.TextColor = Colors.White;
        _headerInitials.TextColor = Colors.White; _headerInitials.HorizontalTextAlignment = TextAlignment.Center; _headerInitials.VerticalTextAlignment = TextAlignment.Center;
        var avatarLayer = new Grid(); avatarLayer.Add(_headerInitials); avatarLayer.Add(_headerAvatar);
        var avatar = new Border { WidthRequest = 62, HeightRequest = 62, StrokeThickness = 2, Stroke = new SolidColorBrush(Colors.White), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 31 }, BackgroundColor = Color.FromArgb("#101D2A"), Content = avatarLayer };
        var header = new Border
        {
            StrokeThickness = 0,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb("#101D2A"), 0),
                    new(Color.FromArgb("#203546"), 1)
                }, new Point(0, 0), new Point(1, 1)),
            Padding = new Thickness(20, 30, 20, 18),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    avatar,
                    new Label { Text = "VALEM", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    _headerName,
                    new Label { Text = user.BranchName ?? "Şube atanmamış", FontSize = 11.5, TextColor = Colors.White }
                }
            }
        };
        var headerLayer = new Grid();
        headerLayer.Add(_headerBackground);
        headerLayer.Add(new BoxView { Color = Color.FromRgba(0, 0, 0, 100) });
        headerLayer.Add(header);
        FlyoutHeader = headerLayer;

        var tabs = new TabBar { Title = "Ana Sayfa", Icon = "tab_home.svg", FlyoutDisplayOptions = FlyoutDisplayOptions.AsSingleItem };
        tabs.Items.Add(CreateTab("Ana Sayfa", "tab_home.svg", new CompanyDashboardPage(api, user)));
        tabs.Items.Add(CreateTab("Araçlar", "tab_car.svg", new CompanyTicketsPage(api, user)));
        tabs.Items.Add(CreateTab("Raporlar", "tab_report.svg", CompanyAccess.CanReport(user)
            ? new ReportsV31Page(api)
            : new RestrictedPage("Raporlar", "Bu hesap için rapor görüntüleme yetkisi tanımlı değil.")));
        tabs.Items.Add(CreateTab("Bildirim", "tab_bell.svg", new NotificationsPage(api, user)));
        tabs.Items.Add(CreateTab("Daha Fazla", "tab_more.svg", new MoreHubPage(api, user)));
        Items.Add(tabs);
        _homeItem = tabs;

        Items.Add(CreateFlyout("Profil", "menu_profile.svg", new CompanyProfilePage(api, user)));
        Items.Add(CreateFlyout("Authenticator", "menu_security.svg", new AuthenticatorSecurityPage(api)));
        if (CompanyAccess.CanManageUsers(user)) Items.Add(CreateFlyout("Ekip", "menu_team.svg", new TeamManagementPage(api, user)));
        if (CompanyAccess.CanManageBranches(user)) Items.Add(CreateFlyout("Firma & Şubeler", "menu_company.svg", new CompanyManagementPage(api)));
        if (CompanyAccess.CanAudit(user)) Items.Add(CreateFlyout("Denetim Kayıtları", "menu_audit.svg", new AuditPage(api)));
        Items.Add(CreateFlyout("Ayarlar", "menu_settings.svg", new ApplicationSettingsPage(api, user)));
        Items.Add(CreateFlyout("Yardım", "menu_help.svg", new ValeHelpPage(api, user)));
        Items.Add(CreateFlyout("İletişim ve Destek", "menu_help.svg", new SupportContactPage()));
        Items.Add(CreateFlyout("Satın Al / Paketim", "menu_security.svg", new PremiumPage(api, user)));
        Items.Add(CreateFlyout("Çıkış", "menu_logout.svg", new LogoutPage(api)));

        _observedItem = tabs;
        var sessionTimer = Dispatcher.CreateTimer();
        sessionTimer.Interval = TimeSpan.FromSeconds(15);
        sessionTimer.Tick += (_, _) => { if (api.IsSessionExpired) api.ExpireSession(); };
        Loaded += (_, _) => sessionTimer.Start();
        Unloaded += (_, _) => sessionTimer.Stop();
        Navigated += async (_, e) =>
        {
            if (e.Source is ShellNavigationSource.ShellSectionChanged or ShellNavigationSource.ShellItemChanged)
            {
                var navigation = CurrentItem?.CurrentItem?.Navigation;
                if (navigation?.NavigationStack.Count > 1) await navigation.PopToRootAsync(false);
            }
        };
        Navigated += (_, _) => TrackTopLevelNavigation();
        _ = LoadHeaderAvatarAsync(api);
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FlyoutIsPresented) && FlyoutIsPresented) _ = LoadHeaderAvatarAsync(api); };
    }

    private async Task LoadHeaderAvatarAsync(ApiClient api)
    {
        if (_loadingAvatar) return;
        try
        {
            _loadingAvatar = true;
            var profile = await api.GetAccountProfileAsync();
            _headerBackground.Source = profile.HeaderBackgroundTheme switch
            {
                "CarHotel" => "theme_car_hotel.png", "CarNeon" => "theme_car_garage.png", "CarTrack" => "theme_car_coast.png",
                "Theme" => profile.BackgroundTheme switch { "CarHotel" => "theme_car_hotel.png", "CarNeon" => "theme_car_garage.png", "CarTrack" => "theme_car_coast.png", "AnimeNeon" => "theme_anime_neon.jpg", "AnimeSunset" => "theme_anime_sunset.jpg", _ => null },
                _ => null
            };
            if (FlyoutHeader is Grid layer && layer.Children.LastOrDefault() is Border panel)
                panel.Background = new SolidColorBrush(Color.FromArgb(profile.ProfileColor == "#2563EB" ? "#101D2A" : profile.ProfileColor).WithAlpha(_headerBackground.Source is null ? 1f : 0.55f));
            _headerName.Text = profile.FullName;
            _headerInitials.Text = string.Concat(profile.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
            if (string.IsNullOrWhiteSpace(profile.ProfilePhotoDataUrl))
            { _headerAvatar.Source = null; _headerAvatar.IsVisible = false; _headerInitials.IsVisible = true; return; }
            var comma = profile.ProfilePhotoDataUrl.IndexOf(',');
            if (comma < 0) return;
            var bytes = Convert.FromBase64String(profile.ProfilePhotoDataUrl[(comma + 1)..]);
            _headerAvatar.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _headerAvatar.IsVisible = true;
            _headerInitials.IsVisible = false;
        }
        catch { /* Header remains usable with initials when profile media is unavailable. */ }
        finally { _loadingAvatar = false; }
    }

    private void TrackTopLevelNavigation()
    {
        var current = CurrentItem;
        if (current is null) return;
        if (!_restoringHistory && _observedItem is not null && !ReferenceEquals(_observedItem, current))
            _topLevelHistory.Push(_observedItem);
        _observedItem = current;
        _restoringHistory = false;
    }

    public async Task<bool> TryNavigateBackAsync()
    {
        if (FlyoutIsPresented)
        {
            FlyoutIsPresented = false;
            return true;
        }

        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
            return true;
        }

        while (_topLevelHistory.Count > 0)
        {
            var previous = _topLevelHistory.Pop();
            if (ReferenceEquals(previous, CurrentItem)) continue;
            _restoringHistory = true;
            CurrentItem = previous;
            return true;
        }

        if (_homeItem is not null && !ReferenceEquals(CurrentItem, _homeItem))
        {
            _restoringHistory = true;
            CurrentItem = _homeItem;
            return true;
        }

        return false;
    }

    private static Tab CreateTab(string title, string icon, Page page)
    {
        page.Title = title;
        var tab = new Tab { Title = title, Icon = icon };
        tab.Items.Add(new ShellContent { Title = title, Icon = icon, Content = page });
        return tab;
    }

    private static FlyoutItem CreateFlyout(string title, string icon, Page page)
    {
        page.Title = title;
        return new FlyoutItem
        {
            Title = title,
            Icon = icon,
            Items = { new ShellContent { Title = title, Icon = icon, Content = page } }
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
                    UiKit.Label("Personeliniz firma ve şube kodlarıyla başvurabilir. Katılım bilgilerini paylaşın; başvuruyu Ekip ekranından onaylayın.", 12.5, false, true),
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
                if (!string.IsNullOrWhiteSpace(branch.CompanyCode))
                {
                    var invite = UiKit.Label($"Firma kodu: {branch.CompanyCode} • Şube kodu: {branch.Code}", 12, true);
                    stack.Add(invite);
                    var copy = UiKit.TextButton("Katılım bilgilerini kopyala");
                    copy.Clicked += async (_, _) =>
                    {
                        await Clipboard.Default.SetTextAsync($"VALEM uygulamasında 'Mevcut firmama katıl' seçeneğini açın. Firma kodu: {branch.CompanyCode}, şube kodu: {branch.Code}.");
                        await DisplayAlertAsync("Katılım bilgileri", "Firma ve şube kodu panoya kopyalandı.", "Tamam");
                    };
                    stack.Add(copy);
                }
                _list.Add(UiKit.Card(stack));
            }
            if (branches.Count == 0) _list.Add(UiKit.Label("Aktif şube bulunamadı.", 13, false, true));
        }
        catch (Exception ex) { await DisplayAlertAsync("Şubeler", UserMessages.For(ex), "Tamam"); }
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
            await DisplayAlertAsync("Şube oluşturuldu", $"Şube kodu: {created.Code}. Liste üzerinden firma ve şube kodunu personelinizle paylaşabilirsiniz.", "Tamam");
            await RefreshAsync();
        }
        catch (Exception ex) { await DisplayAlertAsync("Şube oluşturulamadı", UserMessages.For(ex), "Tamam"); }
    }
}

public sealed class ValeHelpPage : ContentPage
{
    public ValeHelpPage(ApiClient api, UserDto user)
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
                    UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { UiKit.Label("Personel katılımı", 16, true), UiKit.Label("Firma yöneticinizden firma ve şube kodlarını alın. Hesap Oluştur → Mevcut firmama katıl ekranından başvurun. Firmanın yöneticisi onayladığında erişiminiz açılır.", 12.5, false, true) } }),
                    UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { UiKit.Label("Araç ücreti", 16, true), UiKit.Label("Aktif aracın tahmini ücreti araç detayında anlık gösterilir; kesin tahsilat teslim anında hesaplanır.", 12.5, false, true) } }),
                    UiKit.Card(new VerticalStackLayout { Spacing = 6, Children = { UiKit.Label("Bildirim ve güvenlik", 16, true), UiKit.Label("Google/Microsoft Authenticator TOTP, e-posta ile giriş ve gerçek Firebase push bildirimleri kullanılabilir.", 12.5, false, true) } }),
                    GuideButton(),
                    ContactButton()
                }
            }
        };

        View GuideButton()
        {
            var button = UiKit.PrimaryButton("Görsel Kullanım Kılavuzunu Aç");
            button.Clicked += async (_, _) => await Navigation.PushAsync(new UserGuidePage(api, user));
            return button;
        }

        View ContactButton()
        {
            var button = UiKit.SecondaryButton("İletişim ve Destek");
            button.Clicked += async (_, _) => await Navigation.PushAsync(new SupportContactPage());
            return button;
        }
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_done) return;
        _done = true;
        await _api.LogoutAsync();
        App.ShowLogin();
    }
}

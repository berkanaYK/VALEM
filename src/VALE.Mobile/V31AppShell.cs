using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class V31AppShell : Shell
{
    private bool _exitPromptOpen;

    public V31AppShell(ApiClient api, UserDto user)
    {
        FlyoutBehavior = FlyoutBehavior.Flyout;
        FlyoutWidth = 310;
        Title = "VALE";
        Shell.SetTabBarBackgroundColor(this, ThemeService.Palette.Card);
        Shell.SetTabBarTitleColor(this, ThemeService.Palette.Accent);
        Shell.SetTabBarUnselectedColor(this, ThemeService.Palette.Secondary);
        Shell.SetNavBarHasShadow(this, false);
        Shell.SetBackgroundColor(this, ThemeService.Palette.Card);
        Shell.SetTitleColor(this, ThemeService.Palette.Text);
        Shell.SetForegroundColor(this, ThemeService.Palette.Text);

        var tabs = new TabBar { Title = "Ana Menü", Icon = Icon("☰") };
        tabs.Items.Add(CreateTab("Ana", "⌂", new CompanyDashboardPage(api, user)));
        tabs.Items.Add(CreateTab("Araçlar", "▣", new CompanyTicketsPage(api, user)));
        if (CompanyAccess.CanReport(user))
            tabs.Items.Add(CreateTab("Rapor", "▥", new V31ReportsPage(api)));
        tabs.Items.Add(CreateTab("Bildirim", "●", new NotificationCenterPage(api)));
        tabs.Items.Add(CreateTab("Daha", "⋯", new MoreHubPage(api, user)));
        Items.Add(tabs);

        if (CompanyAccess.CanManageUsers(user))
        {
            Items.Add(CreateFlyout("Onaylar & Davetler", "✓", new ApprovalCenterPage(api, user)));
            Items.Add(CreateFlyout("Ekip Yönetimi", "♟", new TeamManagementPage(api, user)));
        }
        if (CompanyAccess.CanAudit(user))
            Items.Add(CreateFlyout("Denetim Kayıtları", "≡", new AuditPage(api)));
        Items.Add(CreateFlyout("Görünüm & Arka Plan", "◐", new AppearancePageV31()));
    }

    private static Tab CreateTab(string title, string glyph, Page page)
    {
        var content = new ShellContent { Title = title, Icon = Icon(glyph), Content = page };
        var tab = new Tab { Title = title, Icon = Icon(glyph) };
        tab.Items.Add(content);
        return tab;
    }

    private static FlyoutItem CreateFlyout(string title, string glyph, Page page)
    {
        var item = new FlyoutItem { Title = title, Icon = Icon(glyph) };
        item.Items.Add(new ShellContent { Title = title, Icon = Icon(glyph), Content = page });
        return item;
    }

    private static FontImageSource Icon(string glyph) => new()
    {
        Glyph = glyph,
        Size = 20,
        Color = ThemeService.Palette.Secondary
    };

    protected override bool OnBackButtonPressed()
    {
        if (FlyoutIsPresented)
        {
            FlyoutIsPresented = false;
            return true;
        }

        if (Navigation.NavigationStack.Count > 1)
        {
            Dispatcher.Dispatch(async () => await Navigation.PopAsync());
            return true;
        }

        if (!_exitPromptOpen)
        {
            _exitPromptOpen = true;
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    var exit = await DisplayAlertAsync("VALE", "Uygulamadan çıkmak istiyor musunuz?", "Çık", "Kal");
                    if (exit) Platform.CurrentActivity?.Finish();
                }
                finally { _exitPromptOpen = false; }
            });
        }
        return true;
    }
}

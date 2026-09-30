using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class App : Application
{
    private static long _accountSession;
    public App()
    {
        Resources = new ResourceDictionary();
        ThemeService.ApplyStored(this);
        RequestedThemeChanged += (_, _) =>
        {
            if (ThemeService.CurrentMode == ValeThemeMode.System)
                ThemeService.ApplyStored(this);
        };
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(new MainPage()));

    public static void ShowAuthenticated(ApiClient api, UserDto user)
    {
        if (Current?.Windows.FirstOrDefault() is { } window)
        {
            GuidedTour.BeginSession(user);
            var session = ++_accountSession;
            var shell = new ValeAppShellV31(api, user);
            window.Page = shell;
            _ = SyncAccountAsync(api, user, session);
            _ = PushTokenManager.AttachAsync(api);
        }
    }

    public static void ShowLogin()
    {
        _accountSession++;
        if (Current?.Windows.FirstOrDefault() is { } window)
            window.Page = new NavigationPage(new MainPage());
    }

    private static async Task SyncAccountAsync(ApiClient api, UserDto user, long session)
    {
        try
        {
            // The shared demo profile is sample data, not this device's settings.
            if (user.Email != "preview@vale.invalid")
            {
                var profile = await api.GetAccountProfileAsync();
                if (session != _accountSession || !api.IsAuthenticated) return;
                ThemeService.ApplyServerPreferences(profile.PreferredTheme, profile.AccentTheme, profile.BackgroundTheme);
            }
        }
        catch
        {
            // Local preferences remain usable if the profile cannot be refreshed.
        }

        try
        {
            if (session != _accountSession || !api.IsAuthenticated) return;
            var entitlement = await api.GetEntitlementAsync();
            PremiumState.Set(user.Id, entitlement.IsPremium);
        }
        catch { PremiumState.Set(user.Id, false); }

    }
}

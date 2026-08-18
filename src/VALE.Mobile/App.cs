using Microsoft.Maui.Controls;
using VALE.Contracts;

namespace VALE.Mobile;

public sealed class App : Application
{
    public App()
    {
        Resources = new ResourceDictionary();
        ThemeService.ApplyStored(this);
        RequestedThemeChanged += (_, _) =>
        {
            if (ThemeService.CurrentMode == ValeThemeMode.System)
                ThemeService.Apply(ValeThemeMode.System, ThemeService.CurrentAccent);
        };
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(new MainPage()));

    public static void ShowAuthenticated(ApiClient api, UserDto user)
    {
        if (Current?.Windows.FirstOrDefault() is { } window)
        {
            window.Page = new ValeAppShellV31(api, user);
            _ = SyncAccountThemeAsync(api);
        }
    }

    public static void ShowLogin()
    {
        if (Current?.Windows.FirstOrDefault() is { } window)
            window.Page = new NavigationPage(new MainPage());
    }

    private static async Task SyncAccountThemeAsync(ApiClient api)
    {
        try
        {
            var profile = await api.GetAccountProfileAsync();
            ThemeService.ApplyServerPreferences(profile.PreferredTheme, profile.AccentTheme);
        }
        catch
        {
            // Local preferences remain usable if the profile cannot be refreshed.
        }
    }
}

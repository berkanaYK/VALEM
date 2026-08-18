using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace VALE.Mobile;

public enum ValeThemeMode
{
    System,
    Light,
    Dark
}

public enum ValeAccent
{
    Blue,
    Indigo,
    Emerald,
    Orange
}

public sealed record ValePalette(
    Color Page,
    Color Card,
    Color SoftCard,
    Color Text,
    Color Secondary,
    Color Border,
    Color Accent,
    Color Success,
    Color Warning,
    Color Danger);

public static class ThemeService
{
    private const string ThemePreferenceKey = "vale_theme_v3";
    private const string AccentPreferenceKey = "vale_accent_v3";

    public static ValeThemeMode CurrentMode => Enum.TryParse<ValeThemeMode>(
        Preferences.Default.Get(ThemePreferenceKey, nameof(ValeThemeMode.System)),
        true,
        out var mode)
            ? mode
            : ValeThemeMode.System;

    public static ValeAccent CurrentAccent => Enum.TryParse<ValeAccent>(
        Preferences.Default.Get(AccentPreferenceKey, nameof(ValeAccent.Blue)),
        true,
        out var accent)
            ? accent
            : ValeAccent.Blue;

    private static bool IsDark =>
        CurrentMode == ValeThemeMode.Dark ||
        (CurrentMode == ValeThemeMode.System && Application.Current?.RequestedTheme == AppTheme.Dark);

    public static ValePalette Palette => BuildPalette(IsDark, CurrentAccent);

    public static void ApplyStored(Application application) =>
        Apply(application, CurrentMode, CurrentAccent, save: false);

    public static void Apply(ValeThemeMode mode) => Apply(mode, CurrentAccent);

    public static void Apply(ValeThemeMode mode, ValeAccent accent)
    {
        if (Application.Current is { } application)
            Apply(application, mode, accent, save: true);
    }

    public static void ApplyServerPreferences(string theme, string accent)
    {
        var parsedTheme = Enum.TryParse<ValeThemeMode>(theme, true, out var mode)
            ? mode
            : ValeThemeMode.System;
        var parsedAccent = Enum.TryParse<ValeAccent>(accent, true, out var selectedAccent)
            ? selectedAccent
            : ValeAccent.Blue;
        Apply(parsedTheme, parsedAccent);
    }

    private static void Apply(Application application, ValeThemeMode mode, ValeAccent accent, bool save)
    {
        if (save)
        {
            Preferences.Default.Set(ThemePreferenceKey, mode.ToString());
            Preferences.Default.Set(AccentPreferenceKey, accent.ToString());
        }

        application.UserAppTheme = mode switch
        {
            ValeThemeMode.Dark => AppTheme.Dark,
            ValeThemeMode.Light => AppTheme.Light,
            _ => AppTheme.Unspecified
        };

        var dark = mode == ValeThemeMode.Dark ||
                   (mode == ValeThemeMode.System && application.RequestedTheme == AppTheme.Dark);
        var p = BuildPalette(dark, accent);

        application.Resources["ValePage"] = p.Page;
        application.Resources["ValeCard"] = p.Card;
        application.Resources["ValeSoftCard"] = p.SoftCard;
        application.Resources["ValeText"] = p.Text;
        application.Resources["ValeSecondary"] = p.Secondary;
        application.Resources["ValeBorder"] = p.Border;
        application.Resources["ValeBorderBrush"] = new SolidColorBrush(p.Border);
        application.Resources["ValeAccent"] = p.Accent;
        application.Resources["ValeSuccess"] = p.Success;
        application.Resources["ValeWarning"] = p.Warning;
        application.Resources["ValeDanger"] = p.Danger;

        RefreshWindowChrome(application, p);
    }

    private static void RefreshWindowChrome(Application application, ValePalette palette)
    {
        foreach (var window in application.Windows)
            RefreshPageChrome(window.Page, palette);
    }

    private static void RefreshPageChrome(Page? page, ValePalette palette)
    {
        if (page is null) return;

        switch (page)
        {
            case Shell shell:
                Shell.SetTabBarBackgroundColor(shell, palette.Card);
                Shell.SetTabBarTitleColor(shell, palette.Accent);
                Shell.SetTabBarUnselectedColor(shell, palette.Secondary);
                Shell.SetBackgroundColor(shell, palette.Card);
                Shell.SetTitleColor(shell, palette.Text);
                Shell.SetForegroundColor(shell, palette.Text);
                break;

            case TabbedPage tabs:
                tabs.BarBackgroundColor = palette.Card;
                tabs.BarTextColor = palette.Text;
                tabs.SelectedTabColor = palette.Accent;
                tabs.UnselectedTabColor = palette.Secondary;
                foreach (var child in tabs.Children)
                    RefreshPageChrome(child, palette);
                break;

            case NavigationPage navigation:
                navigation.BarBackgroundColor = palette.Card;
                navigation.BarTextColor = palette.Text;
                foreach (var child in navigation.Navigation.NavigationStack)
                    RefreshPageChrome(child, palette);
                break;

            case FlyoutPage flyout:
                RefreshPageChrome(flyout.Flyout, palette);
                RefreshPageChrome(flyout.Detail, palette);
                break;
        }
    }

    private static ValePalette BuildPalette(bool dark, ValeAccent accent)
    {
        var accentColor = accent switch
        {
            ValeAccent.Indigo => Color.FromArgb(dark ? "#818CF8" : "#4F46E5"),
            ValeAccent.Emerald => Color.FromArgb(dark ? "#34D399" : "#059669"),
            ValeAccent.Orange => Color.FromArgb(dark ? "#FB923C" : "#EA580C"),
            _ => Color.FromArgb(dark ? "#60A5FA" : "#2563EB")
        };

        return dark
            ? new ValePalette(
                Color.FromArgb("#0B1220"),
                Color.FromArgb("#111827"),
                Color.FromArgb("#172033"),
                Color.FromArgb("#F8FAFC"),
                Color.FromArgb("#94A3B8"),
                Color.FromArgb("#273449"),
                accentColor,
                Color.FromArgb("#22C55E"),
                Color.FromArgb("#F59E0B"),
                Color.FromArgb("#EF4444"))
            : new ValePalette(
                Color.FromArgb("#F8FAFC"),
                Colors.White,
                Color.FromArgb("#FBFDFF"),
                Color.FromArgb("#0F172A"),
                Color.FromArgb("#64748B"),
                Color.FromArgb("#E5E7EB"),
                accentColor,
                Color.FromArgb("#16A34A"),
                Color.FromArgb("#D97706"),
                Color.FromArgb("#DC2626"));
    }
}

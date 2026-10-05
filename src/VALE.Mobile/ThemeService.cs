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

public enum ValeBackgroundTheme
{
    None,
    AnimeNeon,
    AnimeSunset,
    CarNeon,
    CarTrack,
    Custom,
    CarHotel
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
    private const string BackgroundPreferenceKey = "vale_background_v33";
    private const string CustomBackgroundPathKey = "vale_custom_background_v33";
    private static ValePalette? _lastAppliedPalette;
    public static event EventHandler? Changed;

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

    public static ValeBackgroundTheme CurrentBackground => Enum.TryParse<ValeBackgroundTheme>(
        Preferences.Default.Get(BackgroundPreferenceKey, nameof(ValeBackgroundTheme.None)), true, out var background)
            ? background : ValeBackgroundTheme.None;

    public static string? CustomBackgroundPath
    {
        get
        {
            var value = Preferences.Default.Get(CustomBackgroundPathKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
    public static bool HasVisualBackground => CurrentBackground != ValeBackgroundTheme.None;

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
        {
            Apply(application, mode, accent, save: true);
        }
    }

    public static void ApplyServerPreferences(string theme, string accent, string? background = null)
    {
        var parsedTheme = Enum.TryParse<ValeThemeMode>(theme, true, out var mode)
            ? mode
            : ValeThemeMode.System;
        var parsedAccent = Enum.TryParse<ValeAccent>(accent, true, out var selectedAccent)
            ? selectedAccent
            : ValeAccent.Blue;
        if (Enum.TryParse<ValeBackgroundTheme>(background, true, out var parsedBackground))
            Preferences.Default.Set(BackgroundPreferenceKey, parsedBackground.ToString());
        // An older cloud preference must never undo a mode selected locally.
        if (Application.Current is { } application)
            Apply(application, Preferences.Default.ContainsKey(ThemePreferenceKey) ? CurrentMode : parsedTheme, parsedAccent, save: true);
    }

    public static void ApplyBackground(ValeBackgroundTheme background)
    {
        Preferences.Default.Set(BackgroundPreferenceKey, background.ToString());
        if (Application.Current is { } application) Apply(application, CurrentMode, CurrentAccent, save: false);
    }

    public static void SetCustomBackground(string path, ValeAccent suggestedAccent, bool darkImage)
    {
        Preferences.Default.Set(CustomBackgroundPathKey, path);
        Preferences.Default.Set(BackgroundPreferenceKey, nameof(ValeBackgroundTheme.Custom));
        Apply(CurrentMode, suggestedAccent);
    }

    private static void Apply(Application application, ValeThemeMode mode, ValeAccent accent, bool save)
    {
        var previous = _lastAppliedPalette ?? Palette;

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

        RefreshWindowChrome(application, previous, p);
        _lastAppliedPalette = p;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void RefreshWindowChrome(Application application, ValePalette previous, ValePalette palette)
    {
        foreach (var window in application.Windows)
            RefreshPageChrome(window.Page, previous, palette);
    }

    private static void RefreshPageChrome(Page? page, ValePalette previous, ValePalette palette)
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
                foreach (var item in shell.Items)
                foreach (var section in item.Items)
                foreach (var shellContent in section.Items)
                    if (shellContent.Content is Page shellPage)
                        RefreshPageChrome(shellPage, previous, palette);
                break;

            case TabbedPage tabs:
                tabs.BarBackgroundColor = palette.Card;
                tabs.BarTextColor = palette.Text;
                tabs.SelectedTabColor = palette.Accent;
                tabs.UnselectedTabColor = palette.Secondary;
                foreach (var tabChild in tabs.Children)
                    RefreshPageChrome(tabChild, previous, palette);
                break;

            case NavigationPage navigation:
                navigation.BarBackgroundColor = palette.Card;
                navigation.BarTextColor = palette.Text;
                foreach (var navigationChild in navigation.Navigation.NavigationStack)
                    RefreshPageChrome(navigationChild, previous, palette);
                break;

            case FlyoutPage flyout:
                RefreshPageChrome(flyout.Flyout, previous, palette);
                RefreshPageChrome(flyout.Detail, previous, palette);
                break;

            case ContentPage contentPage:
                if (contentPage.Content is Element pageContent)
                    RefreshElementColors(pageContent, previous, palette);
                break;
        }
    }

    private static void RefreshElementColors(Element element, ValePalette previous, ValePalette palette)
    {
        switch (element)
        {
            case Microsoft.Maui.Controls.Switch control when Equals(control.OnColor, previous.Accent):
                control.OnColor = palette.Accent;
                break;

            case Button button when Equals(button.TextColor, previous.Danger):
                button.TextColor = palette.Danger;
                break;

            case GraphicsView graphics:
                graphics.Invalidate();
                break;
        }

        switch (element)
        {
            case Layout layout:
                foreach (var layoutChild in layout.Children)
                    if (layoutChild is Element childElement)
                        RefreshElementColors(childElement, previous, palette);
                break;

            case Border border when border.Content is Element borderChild:
                RefreshElementColors(borderChild, previous, palette);
                break;

            case ScrollView scroll when scroll.Content is Element scrollChild:
                RefreshElementColors(scrollChild, previous, palette);
                break;

            case ContentView view when view.Content is Element viewChild:
                RefreshElementColors(viewChild, previous, palette);
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

        if (CurrentBackground != ValeBackgroundTheme.None)
        {
            return dark ? new ValePalette(
                Color.FromArgb("#101827"), Color.FromRgba(12, 22, 38, 220), Color.FromRgba(21, 34, 54, 225),
                Colors.White, Color.FromArgb("#D4E2F3"), Color.FromRgba(255, 255, 255, 55), accentColor,
                Color.FromArgb("#4ADE80"), Color.FromArgb("#FBBF24"), Color.FromArgb("#FB7185"))
                : new ValePalette(Color.FromArgb("#EEF3FA"), Color.FromRgba(255, 255, 255, 242), Color.FromRgba(235, 242, 251, 245),
                    Color.FromArgb("#14243B"), Color.FromArgb("#465B75"), Color.FromArgb("#BCCBDD"), accentColor,
                    Color.FromArgb("#166534"), Color.FromArgb("#854D0E"), Color.FromArgb("#B91C1C"));
        }

        var tintedPage = accent switch
        {
            ValeAccent.Emerald => dark ? "#071A16" : "#ECFDF5",
            ValeAccent.Indigo => dark ? "#11102A" : "#EEF2FF",
            ValeAccent.Orange => dark ? "#211208" : "#FFF7ED",
            _ => dark ? "#0B1220" : "#EFF6FF"
        };
        var tintedCard = accent switch
        {
            ValeAccent.Emerald => dark ? "#0D241E" : "#F7FFFB",
            ValeAccent.Indigo => dark ? "#19183A" : "#FAFAFF",
            ValeAccent.Orange => dark ? "#2A190D" : "#FFFCF8",
            _ => dark ? "#111827" : "#FFFFFF"
        };
        var tintedSoft = accent switch
        {
            ValeAccent.Emerald => dark ? "#123128" : "#DDFBEF",
            ValeAccent.Indigo => dark ? "#23214A" : "#E0E7FF",
            ValeAccent.Orange => dark ? "#382314" : "#FFEDD5",
            _ => dark ? "#172033" : "#DBEAFE"
        };

        return dark
            ? new ValePalette(
                Color.FromArgb(tintedPage),
                Color.FromArgb(tintedCard),
                Color.FromArgb(tintedSoft),
                Color.FromArgb("#F8FAFC"),
                Color.FromArgb("#94A3B8"),
                Color.FromArgb("#273449"),
                accentColor,
                Color.FromArgb("#22C55E"),
                Color.FromArgb("#F59E0B"),
                Color.FromArgb("#EF4444"))
            : new ValePalette(
                Color.FromArgb(tintedPage),
                Color.FromArgb(tintedCard),
                Color.FromArgb(tintedSoft),
                Color.FromArgb("#0F172A"),
                Color.FromArgb("#64748B"),
                Color.FromArgb("#E5E7EB"),
                accentColor,
                Color.FromArgb("#16A34A"),
                Color.FromArgb("#D97706"),
                Color.FromArgb("#DC2626"));
    }
}

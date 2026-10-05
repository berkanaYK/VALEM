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
    Copper,
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
        Preferences.Default.Get(AccentPreferenceKey, nameof(ValeAccent.Copper)),
        true,
        out var accent)
            ? accent
            : ValeAccent.Copper;

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

    public static bool IsDark =>
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
            : ValeAccent.Copper;
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
        application.Resources["ValeOnAccent"] = dark ? Color.FromArgb("#101D2A") : Colors.White;
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
            ValeAccent.Indigo => Color.FromArgb(dark ? "#B7A3FF" : "#6344B8"),
            ValeAccent.Emerald => Color.FromArgb(dark ? "#63D2B4" : "#087765"),
            ValeAccent.Orange => Color.FromArgb(dark ? "#FFAD70" : "#A34B16"),
            _ => Color.FromArgb(dark ? "#D89C65" : "#92552F")
        };
        // Opaque surfaces keep data readable with every optional photograph.
        return dark
            ? new ValePalette(Color.FromArgb("#101D2A"), Color.FromArgb("#172837"), Color.FromArgb("#203546"),
                Color.FromArgb("#F6F2EC"), Color.FromArgb("#BAC7D0"), Color.FromArgb("#3D5567"), accentColor,
                Color.FromArgb("#64D6AE"), Color.FromArgb("#F4BC69"), Color.FromArgb("#FF9299"))
            : new ValePalette(Color.FromArgb("#FAF7F2"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#F1EAE1"),
                Color.FromArgb("#142A3B"), Color.FromArgb("#536575"), Color.FromArgb("#B9ADA0"), accentColor,
                Color.FromArgb("#147457"), Color.FromArgb("#885B12"), Color.FromArgb("#B52E40"));
    }
}

using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace VALE.Mobile;

public enum ValeThemeMode
{
    Light,
    Dark
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
    private const string PreferenceKey = "vale_theme_v2";

    public static ValeThemeMode CurrentMode =>
        Enum.TryParse<ValeThemeMode>(Preferences.Default.Get(PreferenceKey, nameof(ValeThemeMode.Light)), out var mode)
            ? mode
            : ValeThemeMode.Light;

    public static ValePalette Palette => CurrentMode == ValeThemeMode.Dark
        ? new ValePalette(
            Color.FromArgb("#0B1020"), Color.FromArgb("#121A2B"), Color.FromArgb("#192338"),
            Color.FromArgb("#F8FAFC"), Color.FromArgb("#94A3B8"), Color.FromArgb("#26354E"),
            Color.FromArgb("#4F8CFF"), Color.FromArgb("#22C55E"), Color.FromArgb("#F59E0B"), Color.FromArgb("#EF4444"))
        : new ValePalette(
            Color.FromArgb("#F6F8FC"), Colors.White, Color.FromArgb("#EEF3FA"),
            Color.FromArgb("#0F172A"), Color.FromArgb("#64748B"), Color.FromArgb("#DDE5F0"),
            Color.FromArgb("#2563EB"), Color.FromArgb("#16A34A"), Color.FromArgb("#D97706"), Color.FromArgb("#DC2626"));

    public static void ApplyStored(Application application) => Apply(application, CurrentMode, save: false);

    public static void Apply(ValeThemeMode mode)
    {
        var application = Application.Current;
        if (application is null) return;
        Apply(application, mode, save: true);
    }

    private static void Apply(Application application, ValeThemeMode mode, bool save)
    {
        if (save) Preferences.Default.Set(PreferenceKey, mode.ToString());
        application.UserAppTheme = mode == ValeThemeMode.Dark ? AppTheme.Dark : AppTheme.Light;

        var p = mode == ValeThemeMode.Dark
            ? new ValePalette(Color.FromArgb("#0B1020"), Color.FromArgb("#121A2B"), Color.FromArgb("#192338"), Color.FromArgb("#F8FAFC"), Color.FromArgb("#94A3B8"), Color.FromArgb("#26354E"), Color.FromArgb("#4F8CFF"), Color.FromArgb("#22C55E"), Color.FromArgb("#F59E0B"), Color.FromArgb("#EF4444"))
            : new ValePalette(Color.FromArgb("#F6F8FC"), Colors.White, Color.FromArgb("#EEF3FA"), Color.FromArgb("#0F172A"), Color.FromArgb("#64748B"), Color.FromArgb("#DDE5F0"), Color.FromArgb("#2563EB"), Color.FromArgb("#16A34A"), Color.FromArgb("#D97706"), Color.FromArgb("#DC2626"));

        application.Resources["ValePage"] = p.Page;
        application.Resources["ValeCard"] = p.Card;
        application.Resources["ValeSoftCard"] = p.SoftCard;
        application.Resources["ValeText"] = p.Text;
        application.Resources["ValeSecondary"] = p.Secondary;
        application.Resources["ValeBorder"] = p.Border;
        application.Resources["ValeAccent"] = p.Accent;
    }
}

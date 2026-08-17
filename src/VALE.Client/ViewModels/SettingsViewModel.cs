using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using VALE.Client.Services;

namespace VALE.Client.ViewModels;

public sealed class SettingsViewModel
{
    private readonly ThemeService _themeService;

    public SettingsViewModel(
        SessionService session,
        BranchContext branchContext,
        ClientSettings settings,
        ThemeService themeService)
    {
        _themeService = themeService;
        UserName = session.User?.FullName ?? "Kullanıcı";
        UserEmail = session.User?.Email ?? string.Empty;
        BranchName = branchContext.ActiveBranch?.Name ?? session.User?.BranchName ?? "Şube atanmamış";
        ApiBaseUrl = settings.ApiBaseUrl;
        UseSystemThemeCommand = new RelayCommand(() => _themeService.Apply(ElementTheme.Default));
        UseLightThemeCommand = new RelayCommand(() => _themeService.Apply(ElementTheme.Light));
        UseDarkThemeCommand = new RelayCommand(() => _themeService.Apply(ElementTheme.Dark));
    }

    public string UserName { get; }
    public string UserEmail { get; }
    public string BranchName { get; }
    public string ApiBaseUrl { get; }
    public IRelayCommand UseSystemThemeCommand { get; }
    public IRelayCommand UseLightThemeCommand { get; }
    public IRelayCommand UseDarkThemeCommand { get; }
}

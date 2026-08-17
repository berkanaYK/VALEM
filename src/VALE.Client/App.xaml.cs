using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using VALE.Client.Services;
using VALE.Client.ViewModels;

namespace VALE.Client;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow Window { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        Window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var settings = ClientSettings.Load();
        var services = new ServiceCollection();

        services.AddSingleton(settings);
        services.AddSingleton<SessionService>();
        services.AddSingleton<BranchContext>();
        services.AddSingleton<ApiClient>();
        services.AddSingleton<ThemeService>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TicketsViewModel>();
        services.AddTransient<CheckInViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AdministrationViewModel>();

        return services.BuildServiceProvider();
    }
}

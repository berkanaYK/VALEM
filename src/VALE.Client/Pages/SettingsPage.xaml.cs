using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;

namespace VALE.Client.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}

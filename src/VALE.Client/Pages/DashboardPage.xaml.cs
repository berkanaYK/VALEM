using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;

namespace VALE.Client.Pages;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        InitializeComponent();
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();
}


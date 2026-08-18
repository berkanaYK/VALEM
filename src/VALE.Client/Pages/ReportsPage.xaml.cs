using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;

namespace VALE.Client.Pages;

public sealed partial class ReportsPage : Page
{
    private bool _loadedOnce;

    public ReportsViewModel ViewModel { get; }

    public ReportsPage()
    {
        ViewModel = App.Services.GetRequiredService<ReportsViewModel>();
        InitializeComponent();
    }

    private async void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedIndex < 0)
        {
            return;
        }

        ViewModel.ApplyPreset(PresetCombo.SelectedIndex);
        if (_loadedOnce && PresetCombo.SelectedIndex != 4)
        {
            await ViewModel.LoadCommand.ExecuteAsync(null);
        }
    }
}

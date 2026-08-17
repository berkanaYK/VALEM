using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;

namespace VALE.Client.Pages;

public sealed partial class AdministrationPage : Page
{
    public AdministrationViewModel ViewModel { get; }

    public AdministrationPage()
    {
        ViewModel = App.Services.GetRequiredService<AdministrationViewModel>();
        InitializeComponent();
    }

    private async void AdministrationPage_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private void NewUserPassword_PasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.UserPassword = NewUserPassword.Password;

}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;

namespace VALE.Client.Pages;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        InitializeComponent();
        ViewModel.LoginSucceeded += user => App.Window.ShowShell(user);
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.Password = PasswordInput.Password;
}


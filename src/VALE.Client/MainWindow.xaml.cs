using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VALE.Client.Pages;
using VALE.Contracts;
using Windows.Graphics;

namespace VALE.Client;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "VALE";
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(1280, 820));
        RootFrame.Navigate(typeof(LoginPage));
    }

    public void ShowShell(UserDto user) => RootFrame.Navigate(typeof(ShellPage), user);

    public void SignOut()
    {
        App.Services.GetRequiredService<Services.SessionService>().Clear();
        App.Services.GetRequiredService<Services.BranchContext>().Clear();
        RootFrame.Navigate(typeof(LoginPage));
    }
}

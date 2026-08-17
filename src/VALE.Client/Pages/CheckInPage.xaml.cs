using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VALE.Client.ViewModels;

namespace VALE.Client.Pages;

public sealed partial class CheckInPage : Page
{
    public CheckInViewModel ViewModel { get; }

    public CheckInPage()
    {
        ViewModel = App.Services.GetRequiredService<CheckInViewModel>();
        InitializeComponent();
    }
}


using Microsoft.Maui.Controls;

namespace VALE.Mobile;

public sealed class App : Application
{
    public App()
    {
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var navigation = new NavigationPage(new MainPage())
        {
            BarBackgroundColor = Color.FromArgb("#0B1220"),
            BarTextColor = Colors.White
        };

        return new Window(navigation);
    }
}

using Microsoft.UI.Xaml;

namespace VALE.Client.Services;

public sealed class ThemeService
{
    public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;

    public void Apply(ElementTheme theme)
    {
        CurrentTheme = theme;
        if (App.Window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }
}


using Android.App;
using Android.Content.PM;
using Android.Widget;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace VALE.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private DateTimeOffset _lastBackPress = DateTimeOffset.MinValue;

#pragma warning disable CS0672
    public override void OnBackPressed()
#pragma warning restore CS0672
    {
        var root = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
        var navigation = root switch
        {
            Shell shell => shell.Current?.Navigation ?? shell.Navigation,
            NavigationPage navPage => navPage.Navigation,
            _ => root?.Navigation
        };

        if (navigation is not null && navigation.NavigationStack.Count > 1)
        {
            MainThread.BeginInvokeOnMainThread(async () => await navigation.PopAsync());
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastBackPress <= TimeSpan.FromSeconds(2))
        {
            base.OnBackPressed();
            return;
        }

        _lastBackPress = now;
        Toast.MakeText(this, "Uygulamadan çıkmak için geri tuşuna tekrar basın", ToastLength.Short)?.Show();
    }
}

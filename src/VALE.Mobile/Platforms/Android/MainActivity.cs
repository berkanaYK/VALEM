using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using AndroidX.Activity;
using Firebase.Messaging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace VALE.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[Register("com.berkanayk.vale.MainActivity")]
public sealed class MainActivity : MauiAppCompatActivity
{
    private DateTimeOffset _lastBackPress = DateTimeOffset.MinValue;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OnBackPressedDispatcher.AddCallback(new ValeBackPressedCallback(this));
        ValeFirebaseMessagingService.EnsureChannel(this);

        try
        {
            var app = Firebase.FirebaseApp.InitializeApp(this);
            if (app is not null)
                FirebaseMessaging.Instance.Register();
        }
        catch
        {
            // Firebase resources are injected only in configured release builds.
        }
    }

    private void HandleBackPressed()
    {
        var root = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
        var navigation = root switch
        {
            Shell => Shell.Current?.Navigation,
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
            FinishAfterTransition();
            return;
        }

        _lastBackPress = now;
        Toast.MakeText(this, "Uygulamadan çıkmak için geri tuşuna tekrar basın", ToastLength.Short)?.Show();
    }

    private sealed class ValeBackPressedCallback(MainActivity activity) : OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed() => activity.HandleBackPressed();
    }
}

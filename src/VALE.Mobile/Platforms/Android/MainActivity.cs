using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Gms.Tasks;
using Firebase.Messaging;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace VALE.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity, IOnSuccessListener
{
    private DateTimeOffset _lastBackPress = DateTimeOffset.MinValue;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ValeFirebaseMessagingService.EnsureChannel(this);

        try
        {
            var app = Firebase.FirebaseApp.InitializeApp(this);
            if (app is not null)
                FirebaseMessaging.Instance.GetToken().AddOnSuccessListener(this);
        }
        catch
        {
            // Firebase resources are injected only in configured release builds.
        }
    }

    public void OnSuccess(Java.Lang.Object? result)
    {
        if (result?.ToString() is { Length: > 20 } token)
            _ = PushTokenManager.UpdateTokenAsync(token);
    }

#pragma warning disable CS0672
    public override void OnBackPressed()
#pragma warning restore CS0672
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
            base.OnBackPressed();
            return;
        }

        _lastBackPress = now;
        Toast.MakeText(this, "Uygulamadan çıkmak için geri tuşuna tekrar basın", ToastLength.Short)?.Show();
    }
}

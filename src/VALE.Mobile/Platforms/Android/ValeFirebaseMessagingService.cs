using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Firebase.Messaging;
using System.Runtime.Versioning;

namespace VALE.Mobile;

[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public sealed class ValeFirebaseMessagingService : FirebaseMessagingService
{
    public const string ChannelId = "vale_general";

    public override void OnRegistered(string installationId)
    {
        base.OnRegistered(installationId);
        _ = PushTokenManager.UpdateTokenAsync(installationId);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        var notification = message.GetNotification();
        var title = notification?.Title ?? GetData(message, "title") ?? "VALE";
        var body = notification?.Body ?? GetData(message, "body") ?? "Yeni bir bildiriminiz var.";
        ShowNotification(title, body, message.Data);
    }

    public static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        EnsureChannelForAndroidO(context);
    }

    [SupportedOSPlatform("android26.0")]
    private static void EnsureChannelForAndroidO(Context context)
    {
        var manager = (NotificationManager?)context.GetSystemService(NotificationService);
        if (manager is null || manager.GetNotificationChannel(ChannelId) is not null) return;

        var channel = new NotificationChannel(ChannelId, "VALE Bildirimleri", NotificationImportance.High)
        {
            Description = "Araç teslim istekleri, personel onayları ve önemli VALE bildirimleri"
        };
        channel.EnableVibration(true);
        manager.CreateNotificationChannel(channel);
    }

    private void ShowNotification(string title, string body, IDictionary<string, string> data)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            return;

        EnsureChannel(this);

        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        foreach (var item in data)
            intent.PutExtra(item.Key, item.Value);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= PendingIntentFlags.Immutable;
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, flags);
        if (pendingIntent is null) return;

        using var style = new NotificationCompat.BigTextStyle();
        style.BigText(body);
        using var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetSmallIcon(Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(title);
        builder.SetContentText(body);
        builder.SetStyle(style);
        builder.SetPriority(NotificationCompat.PriorityHigh);
        builder.SetAutoCancel(true);
        builder.SetContentIntent(pendingIntent);

        var builtNotification = builder.Build();
        var manager = NotificationManagerCompat.From(this);
        if (builtNotification is null || manager is null) return;
        manager.Notify((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue), builtNotification);
    }

    private static string? GetData(RemoteMessage message, string key) =>
        message.Data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

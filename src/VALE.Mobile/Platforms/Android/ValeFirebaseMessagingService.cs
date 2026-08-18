using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Firebase.Messaging;

namespace VALE.Mobile;

[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public sealed class ValeFirebaseMessagingService : FirebaseMessagingService
{
    public const string ChannelId = "vale_general";

    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        _ = PushTokenManager.UpdateTokenAsync(token);
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
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
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
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            return;

        EnsureChannel(this);

        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        foreach (var item in data)
            intent.PutExtra(item.Key, item.Value);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M) flags |= PendingIntentFlags.Immutable;
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, flags);

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent);

        NotificationManagerCompat.From(this).Notify((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue), builder.Build());
    }

    private static string? GetData(RemoteMessage message, string key) =>
        message.Data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

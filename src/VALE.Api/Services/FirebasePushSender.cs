using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using VALE.Api.Configuration;
using VALE.Api.Data;

namespace VALE.Api.Services;

public sealed class FirebaseAppProvider(IOptions<FirebaseOptions> options, ILogger<FirebaseAppProvider> logger)
{
    private readonly FirebaseOptions _options = options.Value;
    private readonly object _gate = new();
    private FirebaseMessaging? _messaging;
    private bool _initialized;

    public bool IsConfigured => TryGetMessaging() is not null;

    public FirebaseMessaging? TryGetMessaging()
    {
        if (_initialized) return _messaging;

        lock (_gate)
        {
            if (_initialized) return _messaging;
            _initialized = true;

            if (!_options.IsConfigured)
            {
                logger.LogInformation("Firebase push devre dışı veya eksik yapılandırılmış.");
                return null;
            }

            try
            {
                using var credentialJson = JsonDocument.Parse(_options.ServiceAccountJson);
                var root = credentialJson.RootElement;
                if (!root.TryGetProperty("type", out var type) || type.GetString() != "service_account")
                    throw new InvalidOperationException("Firebase kimlik bilgisi service_account türünde olmalıdır.");
                if (root.TryGetProperty("project_id", out var projectId) &&
                    !string.Equals(projectId.GetString(), _options.ProjectId.Trim(), StringComparison.Ordinal))
                    throw new InvalidOperationException("Firebase service account ProjectId yapılandırmayla eşleşmiyor.");

                var credential = CredentialFactory
                    .FromJson<ServiceAccountCredential>(_options.ServiceAccountJson)
                    .ToGoogleCredential();
                var existing = FirebaseApp.GetInstance("VALE.Push");
                var app = existing ?? FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = _options.ProjectId.Trim()
                }, "VALE.Push");
                _messaging = FirebaseMessaging.GetMessaging(app);
                logger.LogInformation("Firebase Cloud Messaging hazır. ProjectId: {ProjectId}", _options.ProjectId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Firebase Cloud Messaging başlatılamadı.");
                _messaging = null;
            }

            return _messaging;
        }
    }
}

public sealed class FirebasePushSender(
    ValeDbContext db,
    FirebaseAppProvider provider,
    ILogger<FirebasePushSender> logger)
{
    public bool IsConfigured => provider.IsConfigured;

    public async Task<(int Attempted, int Delivered)> SendToUsersAsync(
        Guid companyId,
        IEnumerable<Guid> userIds,
        string title,
        string body,
        string type,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var messaging = provider.TryGetMessaging();
        if (messaging is null) return (0, 0);

        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return (0, 0);

        var registrations = await db.PushRegistrations
            .Where(x => x.CompanyId == companyId && x.IsActive && ids.Contains(x.UserId))
            .OrderByDescending(x => x.LastSeenAt)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        foreach (var registration in registrations)
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    ["type"] = type,
                    ["companyId"] = companyId.ToString(),
                    ["branchId"] = branchId?.ToString() ?? string.Empty
                };

                var message = new Message
                {
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = data,
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        RestrictedPackageName = "com.berkanayk.vale",
                        Notification = new AndroidNotification
                        {
                            ChannelId = "vale_general",
                            Sound = "default",
                            Priority = NotificationPriority.HIGH
                        }
                    }
                };
                SetTarget(message, registration.Token);

                await messaging.SendAsync(message, cancellationToken);
                delivered++;
            }
            catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                registration.IsActive = false;
                logger.LogInformation("Geçersiz FCM kaydı devre dışı bırakıldı. RegistrationId: {RegistrationId}", registration.Id);
            }
            catch (FirebaseMessagingException ex)
            {
                logger.LogWarning(ex, "FCM bildirimi gönderilemedi. RegistrationId: {RegistrationId}, Error: {ErrorCode}", registration.Id, ex.MessagingErrorCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push bildirimi gönderilemedi. RegistrationId: {RegistrationId}", registration.Id);
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);

        return (registrations.Count, delivered);
    }

    private static void SetTarget(Message message, string registrationId)
    {
        if (IsFirebaseInstallationId(registrationId))
        {
            message.Fid = registrationId;
            return;
        }

        // 3.1.x clients uploaded legacy FCM registration tokens. Keep them working
        // during the 3.2 rollout; all new Android clients register a FID instead.
#pragma warning disable CS0618
        message.Token = registrationId;
#pragma warning restore CS0618
    }

    private static bool IsFirebaseInstallationId(string value) =>
        value.Length is >= 20 and <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}

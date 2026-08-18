using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using VALE.Contracts;

namespace VALE.Mobile;

public static class PushTokenManager
{
    private const string TokenPreference = "vale_fcm_token_v1";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static ApiClient? _api;

    // The Firebase token survives logout locally, but its server registration is deactivated.
    // A later authenticated session safely re-registers the same token for the new user.
    public static string? CurrentToken => Preferences.Default.Get(TokenPreference, string.Empty) is { Length: > 0 } token ? token : null;

    public static async Task AttachAsync(ApiClient api, CancellationToken cancellationToken = default)
    {
        _api = api;
        var token = CurrentToken;
        if (!string.IsNullOrWhiteSpace(token))
            await RegisterAsync(token, cancellationToken);
    }

    public static async Task UpdateTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var normalized = token.Trim();
        Preferences.Default.Set(TokenPreference, normalized);
        if (_api is { IsAuthenticated: true })
            await RegisterAsync(normalized, cancellationToken);
    }

    public static async Task DetachAsync(CancellationToken cancellationToken = default)
    {
        var api = _api;
        var token = CurrentToken;
        _api = null;

        if (api is not { IsAuthenticated: true } || string.IsNullOrWhiteSpace(token)) return;

        try
        {
            await api.UnregisterPushTokenAsync(CreateRequest(token), cancellationToken);
        }
        catch
        {
            // Logout must still work if the network or push endpoint is unavailable.
        }
    }

    private static async Task RegisterAsync(string token, CancellationToken cancellationToken)
    {
        var api = _api;
        if (api is not { IsAuthenticated: true }) return;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await api.RegisterPushTokenAsync(CreateRequest(token), cancellationToken);
        }
        catch
        {
            // Token is kept locally and will be retried on the next login/token refresh.
        }
        finally
        {
            Gate.Release();
        }
    }

    private static PushRegistrationRequest CreateRequest(string token) =>
        new(token, "android", $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}".Trim());
}

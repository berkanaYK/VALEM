using VALE.Contracts;

namespace VALE.Client.Services;

public sealed class SessionService
{
    public string? AccessToken { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public UserDto? User { get; private set; }
    public bool IsAuthenticated => AccessToken is not null && User is not null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Set(LoginResponse response)
    {
        AccessToken = response.AccessToken;
        ExpiresAt = response.ExpiresAt;
        User = response.User;
    }

    public void Clear()
    {
        AccessToken = null;
        ExpiresAt = default;
        User = null;
    }
}


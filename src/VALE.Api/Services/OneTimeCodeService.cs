using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class OneTimeCodeService(UserManager<AppUser> userManager, IOptions<JwtOptions> jwtOptions)
{
    private const string LoginProvider = "VALE";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwtOptions.Value.Key);

    public async Task<string> CreateAsync(AppUser user, string purpose)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        var expires = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var hash = ComputeHash(user.Id, purpose, code, expires);
        var result = await userManager.SetAuthenticationTokenAsync(user, LoginProvider, TokenName(purpose), $"{expires}.{hash}");
        if (!result.Succeeded) throw new InvalidOperationException("Tek kullanımlık kod kaydedilemedi.");
        return code;
    }

    public async Task<bool> ValidateAndConsumeAsync(AppUser user, string purpose, string code)
    {
        var tokenName = TokenName(purpose);
        var stored = await userManager.GetAuthenticationTokenAsync(user, LoginProvider, tokenName);
        if (string.IsNullOrWhiteSpace(stored)) return false;
        var parts = stored.Split('.', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) || expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            await userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, tokenName);
            return false;
        }

        var expected = ComputeHash(user.Id, purpose, code.Trim(), expires);
        bool valid;
        try
        {
            valid = CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(parts[1]));
        }
        catch (FormatException) { valid = false; }
        if (!valid) return false;
        await userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, tokenName);
        return true;
    }

    private string ComputeHash(Guid userId, string purpose, string code, long expires)
    {
        var payload = Encoding.UTF8.GetBytes($"{userId:N}:{purpose}:{code}:{expires}");
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(payload));
    }

    private static string TokenName(string purpose) => $"OneTime:{purpose.Trim().ToLowerInvariant()}";
}

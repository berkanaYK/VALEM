using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed class PasswordResetCodeService(
    UserManager<AppUser> userManager,
    IOptions<JwtOptions> jwtOptions)
{
    private const string LoginProvider = "VALE";
    private const string TokenName = "PasswordResetCode";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly byte[] _key = Encoding.UTF8.GetBytes(jwtOptions.Value.Key);

    public async Task<string> CreateAsync(AppUser user)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        var expires = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var hash = ComputeHash(user.Id, code, expires);
        var stored = $"{expires}.{hash}";

        var result = await userManager.SetAuthenticationTokenAsync(user, LoginProvider, TokenName, stored);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Şifre sıfırlama kodu kaydedilemedi.");
        }

        return code;
    }

    public async Task<bool> ValidateAndConsumeAsync(AppUser user, string code)
    {
        var stored = await userManager.GetAuthenticationTokenAsync(user, LoginProvider, TokenName);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('.', 2);
        if (parts.Length != 2 ||
            !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            await userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, TokenName);
            return false;
        }

        var expected = ComputeHash(user.Id, code, expires);
        bool valid;
        try
        {
            valid = CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(parts[1]));
        }
        catch (FormatException)
        {
            valid = false;
        }

        if (!valid)
        {
            return false;
        }

        await userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, TokenName);
        return true;
    }

    private string ComputeHash(Guid userId, string code, long expires)
    {
        var payload = Encoding.UTF8.GetBytes($"{userId:N}:{code}:{expires}");
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(payload));
    }
}

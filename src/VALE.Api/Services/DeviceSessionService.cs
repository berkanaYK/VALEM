using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;

namespace VALE.Api.Services;

public sealed record IssuedDeviceSession(string Token, DateTimeOffset ExpiresAt);
public sealed record RotatedDeviceSession(AppUser User, string Token, DateTimeOffset ExpiresAt);

public sealed class DeviceSessionService(ValeDbContext db, IOptions<DeviceSessionOptions> options)
{
    private readonly TimeSpan _lifetime = TimeSpan.FromDays(options.Value.LifetimeDays);

    public async Task<IssuedDeviceSession> CreateAsync(AppUser user, string? deviceName, CancellationToken cancellationToken)
    {
        if (!user.CompanyId.HasValue)
            throw new InvalidOperationException("Firma kapsamı olmayan kullanıcı için cihaz oturumu üretilemez.");

        var issued = NewToken();
        db.DeviceSessions.Add(new DeviceSession
        {
            CompanyId = user.CompanyId.Value,
            UserId = user.Id,
            TokenHash = Hash(issued.Token),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            DeviceName = CleanDeviceName(deviceName),
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow,
            ExpiresAt = issued.ExpiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task<RotatedDeviceSession?> RotateAsync(string rawToken, string? deviceName, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = Hash(rawToken.Trim());
        var current = await db.DeviceSessions.AsNoTracking()
            .Include(x => x.User).ThenInclude(x => x.Company)
            .Include(x => x.User).ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (current is null || current.RevokedAt.HasValue || current.ExpiresAt <= now ||
            current.User is not { IsActive: true, Company.IsActive: true, Branch.IsActive: true } user ||
            user.CompanyId != current.CompanyId || user.Branch?.CompanyId != current.CompanyId ||
            !string.Equals(user.SecurityStamp, current.SecurityStamp, StringComparison.Ordinal))
            return null;

        var issued = NewToken();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var revoked = await db.DeviceSessions
            // ExpiresAt immutable olduğundan süre kontrolü yukarıdaki doğrulamada yapılır.
            // Buradaki koşullu güncelleme yalnızca aynı belirtecin iki kez kullanılmasını atomik biçimde engeller.
            .Where(x => x.Id == current.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.LastUsedAt, now), cancellationToken);
        if (revoked != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        db.DeviceSessions.Add(new DeviceSession
        {
            CompanyId = current.CompanyId,
            UserId = current.UserId,
            TokenHash = Hash(issued.Token),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            DeviceName = CleanDeviceName(deviceName) ?? current.DeviceName,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = issued.ExpiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RotatedDeviceSession(user, issued.Token, issued.ExpiresAt);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken.Trim());
        var now = DateTimeOffset.UtcNow;
        await db.DeviceSessions.Where(x => x.TokenHash == hash && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.LastUsedAt, now), cancellationToken);
    }

    private IssuedDeviceSession NewToken() =>
        new(WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64)), DateTimeOffset.UtcNow.Add(_lifetime));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string? CleanDeviceName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= 120 ? cleaned : cleaned[..120];
    }
}

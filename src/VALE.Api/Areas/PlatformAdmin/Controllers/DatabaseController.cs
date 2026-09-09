using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;

namespace VALE.Api.Areas.PlatformAdmin.Controllers;

[EnableRateLimiting("session")]
public sealed class DatabaseController(ValeDbContext db, UserManager<AppUser> users,
    TenantBackupService backups, DeveloperQueryService queries, PlatformAuditService audit,
    IDataProtectionProvider protection) : PlatformAdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => await PageAsync(ct);

    [HttpPost, RequestSizeLimit(22_000_000)]
    public async Task<IActionResult> Export(Guid companyId, string password, CancellationToken ct)
    {
        if (!await VerifyAsync(password)) return await PageAsync(ct);
        try
        {
            var backup = await backups.ExportAsync(companyId, ct);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(backup);
            if (bytes.Length > 20_000_000) throw new ApiException(400, "Dosya çok büyük", "Bu firma için sunucudan tam yedek alın. Mobil aktarım sınırı 20 MB.");
            await audit.RecordAsync("database.export", "Company", companyId.ToString(), "Firma işlem kayıtları dışa aktarıldı; kimlik doğrulama verileri dahil edilmedi.", ct);
            return File(bytes, "application/json", $"VALEM-{companyId:D}-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");
        }
        catch (ApiException ex) { ViewBag.Error = ex.Message; return await PageAsync(ct); }
    }

    [HttpPost, RequestSizeLimit(22_000_000)]
    public async Task<IActionResult> Import(IFormFile? file, string password, string? confirmation, CancellationToken ct)
    {
        if (!await VerifyAsync(password)) return await PageAsync(ct);
        if (file is null || file.Length is < 1 or > 20_000_000)
        { ViewBag.Error = "En fazla 20 MB olan VALEM JSON dosyasını seçin."; return await PageAsync(ct); }
        try
        {
            await using var memory = new MemoryStream();
            await file.CopyToAsync(memory, ct);
            var bytes = memory.ToArray();
            var backup = JsonSerializer.Deserialize<TenantBackup>(bytes) ?? throw new JsonException();
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var purpose = protection.CreateProtector("VALEM.TenantImport.v1");
            var prefix = $"{User.FindFirstValue(ClaimTypes.NameIdentifier)}|{backup.CompanyId}|{hash}|";
            var commit = false;
            if (!string.IsNullOrEmpty(confirmation))
            {
                var payload = purpose.Unprotect(confirmation);
                if (!payload.StartsWith(prefix, StringComparison.Ordinal) || !long.TryParse(payload[prefix.Length..], out var expires) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
                    throw new ApiException(400, "Önizleme süresi doldu", "Aynı dosyayla yeniden önizleme yapın.");
                commit = true;
            }
            var result = await backups.RestoreAsync(backup, commit, ct);
            await audit.RecordAsync(commit ? "database.import" : "database.import.preview", "Company", backup.CompanyId.ToString(), $"{result.Added} yeni kayıt; {result.Skipped} mevcut kayıt korundu.", ct);
            ViewBag.Message = commit ? $"Aktarım tamamlandı: {result.Added} kayıt eklendi. {result.Skipped} mevcut kayıt değiştirilmedi." : $"Önizleme başarılı: {backup.CompanyName}. {result.Added} kayıt eklenecek, {result.Skipped} mevcut kayıt korunacak. Uygulamak için aynı dosyayı tekrar seçin ve parolanızla onaylayın.";
            if (!commit) ViewBag.Confirmation = purpose.Protect(prefix + DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
        }
        catch (Exception ex) when (ex is JsonException or ApiException or DbUpdateException or InvalidOperationException or CryptographicException or FormatException or ArgumentException)
        {
            ViewBag.Error = ex is ApiException known ? known.Message : "Dosya doğrulanamadı veya kayıt ilişkileri uygun değil. Hiçbir kayıt aktarılmadı. Özgün dosyayı ve firma/şube ilişkilerini kontrol edin.";
        }
        return await PageAsync(ct);
    }

    [HttpPost, RequestSizeLimit(16_000)]
    public async Task<IActionResult> Query(string? sql, string password, CancellationToken ct)
    {
        if (!await VerifyAsync(password)) return await PageAsync(ct);
        try
        {
            // Audit before execution; query text may contain private customer information.
            await audit.RecordAsync("database.query", "Database", null, "Süre ve satır sınırı olan salt okunur sorgu istendi.", ct);
            ViewBag.Result = await queries.QueryAsync(sql ?? "", ct);
        }
        catch (ApiException ex) { ViewBag.Error = ex.Message; }
        catch (Npgsql.NpgsqlException) { ViewBag.Error = "Sorgu çalıştırılamadı. SQL söz dizimini, okuma yetkisini ve 5 saniyelik süre sınırını kontrol edin."; }
        ViewBag.Sql = sql;
        return await PageAsync(ct);
    }

    private async Task<bool> VerifyAsync(string? password)
    {
        var user = await users.GetUserAsync(User);
        if (user is null || string.IsNullOrEmpty(password) || password.Length > 128 || await users.IsLockedOutAsync(user))
        { ViewBag.Error = "Geliştirici parolanızı doğrulayın. Kilitli hesaplarda bir süre bekleyin."; return false; }
        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            ViewBag.Error = "Geliştirici parolası doğru değil.";
            await audit.RecordAsync("database.access.denied", "User", user.Id.ToString(), "Veritabanı işleminde parola doğrulaması başarısız.");
            return false;
        }
        await users.ResetAccessFailedCountAsync(user);
        return true;
    }

    private async Task<IActionResult> PageAsync(CancellationToken ct)
    {
        ViewBag.QueryEnabled = queries.Enabled;
        return View("Index", await db.Companies.AsNoTracking().Where(x => !x.IsDemo).OrderBy(x => x.Name).Take(1000).ToListAsync(ct));
    }
}

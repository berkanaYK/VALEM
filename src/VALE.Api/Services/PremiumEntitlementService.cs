using System.Security.Cryptography;
using System.Text;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Contracts;

namespace VALE.Api.Services;

public sealed record GooglePlayVerification(
    bool Purchased,
    bool Acknowledged,
    string? OrderId,
    string? ObfuscatedAccountId,
    DateTimeOffset? PurchasedAt);

public interface IGooglePlayPurchaseVerifier
{
    Task<GooglePlayVerification> VerifyAsync(string productId, string purchaseToken, CancellationToken cancellationToken);
    Task AcknowledgeAsync(string productId, string purchaseToken, CancellationToken cancellationToken);
}

public sealed class GooglePlayPurchaseVerifier(IOptions<BillingOptions> options) : IGooglePlayPurchaseVerifier
{
    private readonly BillingOptions _options = options.Value;

    public async Task<GooglePlayVerification> VerifyAsync(string productId, string purchaseToken, CancellationToken cancellationToken)
    {
        EnsureConfigured(productId);
        using var publisher = CreatePublisher();
        var purchase = await publisher.Purchases.Products.Get(_options.PackageName, productId, purchaseToken)
            .ExecuteAsync(cancellationToken);
        DateTimeOffset? purchasedAt = purchase.PurchaseTimeMillis.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseTimeMillis.Value)
            : null;
        return new GooglePlayVerification(
            purchase.PurchaseState == 0,
            purchase.AcknowledgementState == 1,
            purchase.OrderId,
            purchase.ObfuscatedExternalAccountId,
            purchasedAt);
    }

    public async Task AcknowledgeAsync(string productId, string purchaseToken, CancellationToken cancellationToken)
    {
        EnsureConfigured(productId);
        using var publisher = CreatePublisher();
        await publisher.Purchases.Products.Acknowledge(
            new ProductPurchasesAcknowledgeRequest(), _options.PackageName, productId, purchaseToken)
            .ExecuteAsync(cancellationToken);
    }

    private AndroidPublisherService CreatePublisher()
    {
        GoogleCredential credential;
        try
        {
            credential = CredentialFactory.FromJson<ServiceAccountCredential>(_options.ServiceAccountJson)
                .ToGoogleCredential()
                .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
        }
        catch (Exception)
        {
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "Satın alma servisi hazır değil", "Google Play doğrulama hesabı yapılandırılamadı. Lütfen daha sonra tekrar deneyin.");
        }

        return new AndroidPublisherService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "VALEM API"
        });
    }

    private void EnsureConfigured(string productId)
    {
        if (!_options.GooglePlayEnabled || string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "Satın alma yakında açılacak", "Google Play satın alma bağlantısı henüz etkinleştirilmedi.");
        if (!string.Equals(productId, _options.ProductId, StringComparison.Ordinal))
            throw new ApiException(StatusCodes.Status400BadRequest, "Paket tanınmadı", "Güncel VALEM Ömür Boyu paketini seçin.");
    }
}

public sealed class PremiumEntitlementService(
    ValeDbContext db,
    CurrentUserContext currentUser,
    IOptions<BillingOptions> options,
    IGooglePlayPurchaseVerifier verifier,
    IDataProtectionProvider dataProtection,
    AuditService audit)
{
    private const string Plan = "PremiumLifetime";
    private static readonly string[] Features =
    [
        "Sınırsız araç kabul kaydı",
        "Tüm resimli arka plan temaları",
        "Galeriden kişisel arka plan",
        "Premium profil çerçeveleri",
        "Yeni premium özelliklere otomatik erişim"
    ];
    private readonly BillingOptions _options = options.Value;
    private readonly IDataProtector _tokenProtector = dataProtection.CreateProtector("VALEM.GooglePlay.PurchaseToken.v1");

    public async Task<EntitlementDto> GetAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(
            x => x.Id == currentUser.UserId && x.CompanyId == currentUser.CompanyId,
            cancellationToken);
        var entitlement = await db.UserEntitlements.SingleOrDefaultAsync(
            x => x.UserId == currentUser.UserId && x.Plan == Plan && x.IsActive,
            cancellationToken);
        if (entitlement is not null && _options.GooglePlayEnabled &&
            entitlement.Source == "GooglePlay" && entitlement.VerifiedAt < DateTimeOffset.UtcNow.AddHours(-12) &&
            !string.IsNullOrWhiteSpace(entitlement.ProtectedPurchaseToken))
        {
            try
            {
                var token = _tokenProtector.Unprotect(entitlement.ProtectedPurchaseToken);
                var check = await verifier.VerifyAsync(entitlement.ProductId, token, cancellationToken);
                entitlement.VerifiedAt = DateTimeOffset.UtcNow;
                if (!check.Purchased)
                {
                    entitlement.IsActive = false;
                    entitlement.RevokedAt = DateTimeOffset.UtcNow;
                    entitlement = null;
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { /* A temporary Play outage must not lock a paid user out. */ }
        }
        var premium = IsTestAccount(user) || entitlement is not null;
        var used = await UsedRecordCountAsync(cancellationToken);
        return Map(premium, entitlement?.PurchasedAt, used);
    }

    public async Task EnsureCanCreateVehicleRecordAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(
            x => x.Id == currentUser.UserId && x.CompanyId == currentUser.CompanyId,
            cancellationToken);
        if (IsTestAccount(user) || await db.UserEntitlements.AsNoTracking().AnyAsync(
                x => x.UserId == currentUser.UserId && x.Plan == Plan && x.IsActive,
                cancellationToken))
            return;

        var used = await UsedRecordCountAsync(cancellationToken);
        if (used >= _options.DemoVehicleLimit)
            throw new ApiException(StatusCodes.Status402PaymentRequired, "Deneme sınırına ulaştınız",
                $"Ücretsiz kullanımda {_options.DemoVehicleLimit} araç kaydı oluşturabilirsiniz. Eski ve ödeme almamış bir kaydı silerek yer açabilir veya VALEM Ömür Boyu paketini satın alabilirsiniz.");
    }

    public async Task<PurchaseVerificationDto> VerifyAndGrantAsync(
        VerifyGooglePlayPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var token = request.PurchaseToken.Trim();
        var productId = request.ProductId.Trim();
        var verification = await verifier.VerifyAsync(productId, token, cancellationToken);
        if (!verification.Purchased)
            throw new ApiException(StatusCodes.Status409Conflict, "Ödeme tamamlanmadı", "Google Play ödemenizi henüz tamamlanmış olarak bildirmiyor.");
        if (!string.IsNullOrWhiteSpace(verification.ObfuscatedAccountId) &&
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(verification.ObfuscatedAccountId),
                Encoding.UTF8.GetBytes(AccountBinding(currentUser.UserId))))
            throw new ApiException(StatusCodes.Status409Conflict, "Satın alma hesabı eşleşmiyor", "Bu satın alma başka bir VALEM hesabı için başlatılmış.");

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        var claimedByAnotherUser = await db.UserEntitlements.AsNoTracking().AnyAsync(
            x => x.PurchaseTokenHash == tokenHash && x.UserId != currentUser.UserId,
            cancellationToken);
        if (claimedByAnotherUser)
            throw new ApiException(StatusCodes.Status409Conflict, "Satın alma daha önce kullanılmış", "Bu Google Play satın alması başka bir VALEM hesabına bağlı.");

        var entitlement = await db.UserEntitlements.SingleOrDefaultAsync(
            x => x.UserId == currentUser.UserId && x.Plan == Plan,
            cancellationToken);
        if (entitlement is null)
        {
            entitlement = new UserEntitlement
            {
                CompanyId = currentUser.CompanyId,
                UserId = currentUser.UserId,
                Plan = Plan
            };
            db.UserEntitlements.Add(entitlement);
        }

        entitlement.Source = "GooglePlay";
        entitlement.ProductId = productId;
        entitlement.PurchaseTokenHash = tokenHash;
        entitlement.ProtectedPurchaseToken = _tokenProtector.Protect(token);
        entitlement.OrderId = verification.OrderId;
        entitlement.IsActive = true;
        entitlement.PurchasedAt = verification.PurchasedAt ?? entitlement.PurchasedAt;
        entitlement.VerifiedAt = DateTimeOffset.UtcNow;
        entitlement.RevokedAt = null;
        await db.SaveChangesAsync(cancellationToken);

        if (!verification.Acknowledged)
        {
            try { await verifier.AcknowledgeAsync(productId, token, cancellationToken); }
            catch { /* Client also acknowledges; the persisted entitlement prevents loss after payment. */ }
        }

        await audit.RecordAsync(currentUser.UserId, currentUser.BranchId, "billing.premium.granted", "UserEntitlement", entitlement.Id.ToString(), "VALEM Ömür Boyu erişimi Google Play doğrulamasıyla etkinleştirildi.", cancellationToken: cancellationToken);
        return new PurchaseVerificationDto(await GetAsync(cancellationToken), !verification.Acknowledged);
    }

    public static string AccountBinding(Guid userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString("D")))).ToLowerInvariant();

    private bool IsTestAccount(AppUser user) => user.EmailConfirmed && !string.IsNullOrWhiteSpace(user.Email) &&
        _options.TestPremiumEmails.Any(x => string.Equals(x?.Trim(), user.Email.Trim(), StringComparison.OrdinalIgnoreCase));

    private Task<int> UsedRecordCountAsync(CancellationToken cancellationToken) =>
        db.ParkingTickets.AsNoTracking().CountAsync(
            x => x.CompanyId == currentUser.CompanyId && x.CreatedByUserId == currentUser.UserId,
            cancellationToken);

    private EntitlementDto Map(bool premium, DateTimeOffset? purchasedAt, int used)
    {
        var remaining = premium ? int.MaxValue : Math.Max(0, _options.DemoVehicleLimit - used);
        return new EntitlementDto(
            premium,
            premium ? "VALEM Ömür Boyu" : "Ücretsiz Deneme",
            _options.ProductId,
            _options.FallbackDisplayPrice,
            _options.DemoVehicleLimit,
            used,
            remaining,
            premium || remaining > 0,
            purchasedAt,
            premium ? "Tüm özellikler açık" : $"{remaining} ücretsiz araç kaydı kaldı",
            Features);
    }
}

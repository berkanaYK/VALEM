using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public static class PremiumProduct
{
    public const string Id = "valem_premium_lifetime";
    public const int DemoVehicleLimit = 50;
    public const string FallbackPrice = "$5.99";
}

public sealed record EntitlementDto(
    bool IsPremium,
    string Plan,
    string ProductId,
    string DisplayPrice,
    int DemoVehicleLimit,
    int UsedVehicleRecords,
    int RemainingVehicleRecords,
    bool CanCreateVehicleRecord,
    DateTimeOffset? PurchasedAt,
    string StatusText,
    IReadOnlyList<string> PremiumFeatures);

public sealed record VerifyGooglePlayPurchaseRequest(
    [param: Required, MaxLength(120)] string ProductId,
    [param: Required, MinLength(20), MaxLength(4096)] string PurchaseToken);

public sealed record PurchaseVerificationDto(EntitlementDto Entitlement, bool ShouldAcknowledge);

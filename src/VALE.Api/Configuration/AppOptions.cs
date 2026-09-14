using VALE.Contracts;

namespace VALE.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "VALE.Api";
    public string Audience { get; init; } = "VALE.Client";
    public string Key { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 480;
}

public sealed class DeviceSessionOptions
{
    public const string SectionName = "DeviceSessions";

    public int LifetimeDays { get; init; } = 30;
}

public sealed class PlatformAdminOptions
{
    public const string SectionName = "PlatformAdmin";

    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FullName { get; init; } = "VALEM Platform Yöneticisi";
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string AdminFullName { get; init; } = "Sistem Yöneticisi";
    public string DefaultBranchCode { get; init; } = "MRKZ";
    public string DefaultBranchName { get; init; } = "Merkez Şube";
    public string DefaultBranchCity { get; init; } = "Antalya";
}

public sealed class BusinessRulesOptions
{
    public const string SectionName = "BusinessRules";

    public decimal DefaultHourlyRate { get; init; } = 100m;
    public string TimeZoneId { get; init; } = "Europe/Istanbul";
}

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public bool GooglePlayEnabled { get; init; }
    public string PackageName { get; init; } = "com.berkanayk.vale";
    public string ProductId { get; init; } = PremiumProduct.Id;
    public string FallbackDisplayPrice { get; init; } = PremiumProduct.FallbackPrice;
    public int DemoVehicleLimit { get; init; } = PremiumProduct.DemoVehicleLimit;
    public string ServiceAccountJson { get; init; } = string.Empty;
    public string[] TestPremiumEmails { get; init; } = [];
}

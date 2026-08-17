namespace VALE.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "VALE.Api";
    public string Audience { get; init; } = "VALE.Client";
    public string Key { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 480;
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

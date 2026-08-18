using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record TwoFactorLoginRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, MinLength(8), MaxLength(128)] string Password,
    [param: Required, RegularExpression("^[0-9]{6}$")] string Code);

public sealed record EmailCodeRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record EmailCodeVerifyRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, RegularExpression("^[0-9]{6}$")] string Code,
    [param: RegularExpression("^[0-9]{6}$")] string? TwoFactorCode = null);

public sealed record TwoFactorCodeRequest(
    [param: Required, RegularExpression("^[0-9]{6}$")] string Code);

public sealed record TwoFactorStatusDto(bool Enabled, bool HasAuthenticatorKey, int RecoveryCodesLeft);
public sealed record TwoFactorSetupDto(string SharedKey, string AuthenticatorUri);
public sealed record TwoFactorRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

public sealed record AccountProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? EmployeeCode,
    string? JobTitle,
    Guid? BranchId,
    string? BranchName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string PreferredTheme,
    string AccentTheme,
    string ProfileColor,
    bool TwoFactorEnabled);

public sealed record UpdateAccountProfileRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    [param: MaxLength(30)] string? PhoneNumber,
    [param: Required, MaxLength(20)] string PreferredTheme,
    [param: Required, MaxLength(20)] string AccentTheme,
    [param: Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] string ProfileColor);

public sealed record AdminUserDetailDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? EmployeeCode,
    string? JobTitle,
    Guid? BranchId,
    string? BranchName,
    bool IsActive,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed record UpdateAdminUserRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    [param: MaxLength(30)] string? PhoneNumber,
    [param: MaxLength(40)] string? EmployeeCode,
    [param: MaxLength(80)] string? JobTitle,
    Guid BranchId,
    bool IsActive,
    [param: Required, MinLength(1)] IReadOnlyList<string> Roles);

public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? UserId,
    string? UserName,
    Guid? BranchId,
    string? BranchName,
    string Action,
    string EntityType,
    string? EntityId,
    string Detail,
    bool Success);

public sealed record UpdateTicketDetailsRequest(
    [param: Required, MinLength(3), MaxLength(16)] string LicensePlate,
    [param: MaxLength(60)] string? Brand,
    [param: MaxLength(60)] string? Model,
    [param: MaxLength(40)] string? Color,
    [param: Range(1900, 2100)] int? Year,
    [param: MaxLength(30)] string? FuelType,
    [param: MaxLength(30)] string? Transmission,
    [param: MaxLength(120)] string? CustomerName,
    [param: MaxLength(30)] string? CustomerPhone,
    [param: MaxLength(30)] string? KeyTag,
    [param: MaxLength(30)] string? ParkingSpot,
    [param: MaxLength(500)] string? Notes,
    [param: Range(0.01, 1000000)] decimal? HourlyRate,
    string? PhotoBase64,
    bool RemovePhoto = false);

public sealed record DeleteTicketRequest(
    [param: Required, MinLength(3), MaxLength(300)] string Reason);

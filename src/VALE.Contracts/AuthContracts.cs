using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record LoginRequest(
    [param: Required, MaxLength(256)] string Email,
    [param: Required, MinLength(6), MaxLength(128)] string Password,
    bool RememberDevice = false,
    [param: MaxLength(120)] string? DeviceName = null);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserDto User,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null);

public sealed record RefreshSessionRequest(
    [param: Required, MinLength(40), MaxLength(256)] string RefreshToken,
    [param: MaxLength(120)] string? DeviceName = null);

public sealed record SmsCodeRequest([param: Required, MaxLength(30)] string PhoneNumber);
public sealed record SmsVerifyRequest(
    [param: Required, MaxLength(30)] string PhoneNumber,
    [param: Required, RegularExpression("^[0-9]{6}$")] string Code,
    [param: MaxLength(10)] string? TwoFactorCode = null,
    bool RememberDevice = false,
    [param: MaxLength(120)] string? DeviceName = null);
public sealed record ChangeEmailRequest(
    [param: Required, EmailAddress, MaxLength(256)] string NewEmail,
    [param: Required, MaxLength(128)] string CurrentPassword);

public sealed record RegisterRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, MinLength(6), MaxLength(20)] string Password,
    [param: MaxLength(30)] string? PhoneNumber = null,
    [param: MaxLength(20)] string? BranchCode = null,
    [param: MaxLength(40)] string? EmployeeCode = null);

public sealed record RegisterResponse(string Message, bool RequiresApproval);

public sealed record ForgotPasswordRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record ResetPasswordRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, RegularExpression("^[0-9]{6}$")] string Code,
    [param: Required, MinLength(6), MaxLength(20)] string NewPassword);

public sealed record UpdateProfileRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName);

public sealed record ChangePasswordRequest(
    [param: Required, MinLength(6), MaxLength(128)] string CurrentPassword,
    [param: Required, MinLength(6), MaxLength(20)] string NewPassword);

public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    Guid? BranchId,
    string? BranchName,
    IReadOnlyList<string> Roles);

public sealed record CreateUserRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, MinLength(6), MaxLength(20)] string Password,
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    Guid BranchId,
    [param: Required, MinLength(1)] IReadOnlyList<string> Roles);

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record AdminUserDto(
    Guid Id,
    string FullName,
    string Email,
    Guid? BranchId,
    string? BranchName,
    bool IsActive,
    IReadOnlyList<string> Roles)
{
    public string RolesText => string.Join(", ", Roles);
    public string StatusText => IsActive ? "Aktif" : "Onay bekliyor / Pasif";
}

using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record LoginRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserDto User);

public sealed record RegisterRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, MinLength(10), MaxLength(128)] string Password,
    [param: MaxLength(30)] string? PhoneNumber = null,
    [param: MaxLength(20)] string? BranchCode = null,
    [param: MaxLength(40)] string? EmployeeCode = null,
    RegistrationKind Kind = RegistrationKind.Staff,
    [param: MaxLength(30)] string? CompanyCode = null,
    [param: MaxLength(160)] string? CompanyName = null,
    [param: MaxLength(120)] string? BranchName = null,
    [param: MaxLength(80)] string? BranchCity = null,
    [param: MaxLength(300)] string? BranchAddress = null,
    [param: MaxLength(40)] string? InviteCode = null,
    [param: MaxLength(40)] string? RequestedRole = null);

public sealed record RegisterResponse(string Message, bool RequiresApproval);

public sealed record ForgotPasswordRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record ResetPasswordRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, RegularExpression("^[0-9]{6}$")] string Code,
    [param: Required, MinLength(10), MaxLength(128)] string NewPassword);

public sealed record UpdateProfileRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName);

public sealed record ChangePasswordRequest(
    [param: Required, MinLength(8), MaxLength(128)] string CurrentPassword,
    [param: Required, MinLength(10), MaxLength(128)] string NewPassword);

public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    Guid? BranchId,
    string? BranchName,
    IReadOnlyList<string> Roles,
    Guid? CompanyId = null,
    string? CompanyName = null,
    string? CompanyCode = null);

public sealed record CreateUserRequest(
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: Required, MinLength(10), MaxLength(128)] string Password,
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

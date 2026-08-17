using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record LoginRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserDto User);

public sealed record RegisterRequest(
    [property: Required, MinLength(2), MaxLength(120)] string FullName,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(10), MaxLength(128)] string Password);

public sealed record RegisterResponse(string Message, bool RequiresApproval);

public sealed record ForgotPasswordRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record ResetPasswordRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, RegularExpression("^[0-9]{6}$")] string Code,
    [property: Required, MinLength(10), MaxLength(128)] string NewPassword);

public sealed record UpdateProfileRequest(
    [property: Required, MinLength(2), MaxLength(120)] string FullName);

public sealed record ChangePasswordRequest(
    [property: Required, MinLength(8), MaxLength(128)] string CurrentPassword,
    [property: Required, MinLength(10), MaxLength(128)] string NewPassword);

public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    Guid? BranchId,
    string? BranchName,
    IReadOnlyList<string> Roles);

public sealed record CreateUserRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(10), MaxLength(128)] string Password,
    [property: Required, MinLength(2), MaxLength(120)] string FullName,
    Guid BranchId,
    [property: Required, MinLength(1)] IReadOnlyList<string> Roles);

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

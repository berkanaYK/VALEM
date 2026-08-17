using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record LoginRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserDto User);

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
    public string StatusText => IsActive ? "Aktif" : "Pasif";
}

using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public static class LoginMethods
{
    public const string Password = "Password";
    public const string EmailCode = "EmailCode";
    public const string Authenticator = "Authenticator";
    public static readonly string[] All = [Password, EmailCode, Authenticator];
}

public sealed record OwnerRegisterRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: MaxLength(128)] string? Password,
    [param: MaxLength(30)] string? PhoneNumber,
    [param: Required, MinLength(2), MaxLength(160)] string CompanyName,
    [param: Required, MinLength(2), MaxLength(40)] string CompanyCode,
    [param: Required, MinLength(2), MaxLength(120)] string FirstBranchName,
    [param: Required, MinLength(1), MaxLength(20)] string FirstBranchCode,
    [param: MaxLength(80)] string? City = null,
    [param: Required] string LoginMethod = LoginMethods.Password);

public sealed record StaffRegisterRequest(
    [param: Required, MinLength(2), MaxLength(120)] string FullName,
    [param: Required, EmailAddress, MaxLength(256)] string Email,
    [param: MaxLength(128)] string? Password,
    [param: MaxLength(30)] string? PhoneNumber,
    [param: MaxLength(40)] string? CompanyCode,
    [param: MaxLength(20)] string? BranchCode,
    [param: MaxLength(40)] string? InviteCode,
    [param: MaxLength(40)] string? EmployeeCode = null,
    [param: Required] string LoginMethod = LoginMethods.Password);

public sealed record RegistrationRequestDto(
    Guid Id,
    Guid ApplicantUserId,
    string FullName,
    string Email,
    Guid CompanyId,
    string CompanyName,
    Guid BranchId,
    string BranchName,
    string RequestedRole,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? Note);

public sealed record RegistrationDecisionRequest(bool Approve, [param: MaxLength(500)] string? Note = null);

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    string Type,
    bool IsRead,
    DateTimeOffset CreatedAt,
    Guid? BranchId);

public sealed record MarkNotificationReadRequest(bool IsRead = true);

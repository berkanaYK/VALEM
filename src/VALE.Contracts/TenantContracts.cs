using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public enum RegistrationKind
{
    Staff,
    CompanyOwner
}

public enum RegistrationApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed record CompanyDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record RegistrationCompanyDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<RegistrationBranchDto> Branches);

public sealed record RegistrationBranchDto(
    Guid Id,
    string Code,
    string Name,
    string City);

public sealed record RegistrationRequestDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    Guid CompanyId,
    string CompanyName,
    Guid BranchId,
    string BranchName,
    string RequestedRole,
    RegistrationApprovalStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);

public sealed record ReviewRegistrationRequest(
    [param: MaxLength(500)] string? Note = null,
    [param: MaxLength(40)] string? Role = null);

public sealed record CreateCompanyInviteRequest(
    Guid BranchId,
    [param: Required, MaxLength(40)] string Role,
    [param: Range(1, 90)] int ValidDays = 7,
    [param: Range(1, 100)] int MaxUses = 1);

public sealed record CompanyInviteDto(
    Guid Id,
    string Code,
    Guid CompanyId,
    string CompanyName,
    Guid BranchId,
    string BranchName,
    string Role,
    bool IsActive,
    DateTimeOffset ExpiresAt,
    int MaxUses,
    int UsedCount,
    DateTimeOffset CreatedAt);

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string Type,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    string? EntityType,
    string? EntityId);

public sealed record NotificationSummaryDto(
    int UnreadCount,
    IReadOnlyList<NotificationDto> Items);

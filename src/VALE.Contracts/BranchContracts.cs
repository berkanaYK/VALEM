namespace VALE.Contracts;

using System.ComponentModel.DataAnnotations;

public sealed record BranchDto(
    Guid Id,
    string Code,
    string Name,
    string City,
    string Address,
    bool IsActive);

public sealed record CreateBranchRequest(
    [property: Required, MinLength(2), MaxLength(20)] string Code,
    [property: Required, MinLength(2), MaxLength(120)] string Name,
    [property: Required, MinLength(2), MaxLength(80)] string City,
    [property: MaxLength(300)] string? Address);

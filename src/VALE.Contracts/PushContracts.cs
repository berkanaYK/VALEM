using System.ComponentModel.DataAnnotations;

namespace VALE.Contracts;

public sealed record PushRegistrationRequest(
    [param: Required, MinLength(20), MaxLength(4096)] string Token,
    [param: MaxLength(32)] string Platform = "android",
    [param: MaxLength(120)] string? DeviceName = null);

public sealed record PushRegistrationDto(
    Guid Id,
    string Platform,
    string? DeviceName,
    DateTimeOffset LastSeenAt,
    bool IsActive);

public sealed record PushStatusDto(
    bool ServerConfigured,
    int RegisteredDevices);

public sealed record PushTestResponse(
    bool ServerConfigured,
    int Attempted,
    int Delivered);

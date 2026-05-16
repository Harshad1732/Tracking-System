using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

public record UserAdminDto(
    Guid Id,
    int Number,
    string Email,
    string? FullName,
    string Role,
    string? Provider,
    bool IsActive,
    bool HasPassword,
    DateTime CreatedAtUtc);

public record CreateUserRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [MaxLength(120)] string? FullName,
    [Required, MaxLength(40)] string Role,
    [Required, MinLength(8)] string Password);

public record UpdateUserRequest(
    [MaxLength(120)] string? FullName,
    [Required, MaxLength(40)] string Role,
    bool IsActive);

public record ResetUserPasswordRequest([Required, MinLength(8)] string NewPassword);

public record WorkspaceDto(
    Guid Id, string Name, string Slug, DateTime CreatedAtUtc,
    int UserCount, int ShopfloorCount, int PlantCount);

public record UpdateWorkspaceRequest([Required, MaxLength(120)] string Name);

using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

public record UserAdminDto(
    Guid Id,
    int Number,
    string Email,
    string? FullName,
    string? Provider,
    bool IsActive,
    bool HasPassword,
    Guid? DefaultPlantId,
    string? DefaultPlantName,
    bool IsPlatformAdmin,
    IReadOnlyList<UserAssignmentDto> Assignments,
    DateTime CreatedAtUtc);

public record UserAssignmentDto(
    Guid Id,
    Guid RoleId,
    string RoleName,
    bool IsSystemAdmin,
    string ScopeType,
    Guid? ScopeId,
    string? ScopeName);

public record AssignmentInputDto(
    [Required] Guid RoleId,
    [Required, MaxLength(40)] string ScopeType,
    Guid? ScopeId);

public record CreateUserRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [MaxLength(120)] string? FullName,
    [Required, MinLength(8)] string Password,
    Guid? DefaultPlantId,
    IReadOnlyList<AssignmentInputDto>? Assignments);

public record UpdateUserRequest(
    [MaxLength(120)] string? FullName,
    bool IsActive,
    Guid? DefaultPlantId,
    IReadOnlyList<AssignmentInputDto>? Assignments);

public record ResetUserPasswordRequest([Required, MinLength(8)] string NewPassword);

public record WorkspaceDto(
    Guid Id, string Name, string Slug, DateTime CreatedAtUtc,
    int UserCount, int ShopfloorCount, int PlantCount);

public record UpdateWorkspaceRequest([Required, MaxLength(120)] string Name);

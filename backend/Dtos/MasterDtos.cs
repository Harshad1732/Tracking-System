using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

// Plants — Code is server-generated (e.g. PLT-001), not user-supplied.
public record PlantDto(
    Guid Id, int Number, string Code, string Name, string? Address, string? Phone,
    bool IsActive, DateTime CreatedAtUtc);

public record PlantUpsertRequest(
    [Required, MaxLength(120)] string Name,
    [MaxLength(250)] string? Address,
    [MaxLength(30)] string? Phone,
    bool IsActive);

// Processes
public record ProcessDto(
    Guid Id, int Number, Guid PlantId, string PlantName, string Code, string Name,
    int SequenceNo, bool IsActive, DateTime CreatedAtUtc);

public record ProcessUpsertRequest(
    [Required] Guid PlantId,
    [Required, MaxLength(120)] string Name,
    int SequenceNo,
    bool IsActive);

// Employees
public record EmployeeDto(
    Guid Id, int Number, string Code, string Name, string? Mobile, string? Department, string? Designation,
    Guid? PlantId, string? PlantName, Guid? ProcessId, string? ProcessName,
    bool IsActive, DateTime CreatedAtUtc);

public record EmployeeUpsertRequest(
    [Required, MaxLength(120)] string Name,
    [MaxLength(30)] string? Mobile,
    [MaxLength(60)] string? Department,
    [MaxLength(60)] string? Designation,
    Guid? PlantId,
    Guid? ProcessId,
    bool IsActive);

// Customers
public record CustomerDto(
    Guid Id, int Number, string Code, string Name, string? ContactPerson, string? Mobile, string? Email,
    string? Address, bool IsActive, DateTime CreatedAtUtc);

public record CustomerUpsertRequest(
    [Required, MaxLength(120)] string Name,
    [MaxLength(120)] string? ContactPerson,
    [MaxLength(30)] string? Mobile,
    [EmailAddress, MaxLength(150)] string? Email,
    [MaxLength(250)] string? Address,
    bool IsActive);

// Roles — permissions are a list of (resource, action) pairs, not a 5-bit bitmap.
public record RolePermissionDto(string Resource, string Action);

public record RoleDto(
    Guid Id, int Number, string Name, string? Description,
    bool IsSystem, bool IsSystemAdmin, bool IsActive,
    IReadOnlyList<RolePermissionDto> Permissions,
    int AssignedUserCount,
    DateTime CreatedAtUtc);

public record RoleUpsertRequest(
    [Required, MaxLength(60)] string Name,
    [MaxLength(250)] string? Description,
    bool IsActive,
    IReadOnlyList<RolePermissionDto> Permissions);

public record PermResourceDto(Guid Id, string Code, string Name, string? Description, int SortOrder, bool IsSystem);
public record PermActionDto(Guid Id, string Code, string Name, int SortOrder, bool IsSystem);
public record PermissionCatalogDto(
    IReadOnlyList<PermResourceDto> Resources,
    IReadOnlyList<PermActionDto> Actions);

// Shopfloors — code is "STORAGE" / "SF1" / "SF2" generated per-plant.
public record ShopfloorDto(
    Guid Id, int Number, string Code, string Name, int SequenceNo, bool IsStorage,
    string BatchMode,
    Guid? ProcessId, string? ProcessName,
    int SheetCount,
    string? Color,
    bool IsActive, DateTime CreatedAtUtc);

public record ShopfloorUpsertRequest(
    [Required, MaxLength(80)] string Name,
    int SequenceNo,
    bool IsStorage,
    [Required, MaxLength(20)] string BatchMode,
    Guid? ProcessId,
    [MaxLength(7)] string? Color,
    bool IsActive);

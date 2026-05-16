using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

// Plants
public record PlantDto(
    Guid Id, int Number, string Code, string Name, string? Address, string? Phone,
    bool IsActive, DateTime CreatedAtUtc);

public record PlantUpsertRequest(
    [Required, MaxLength(20)] string Code,
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
    [Required, MaxLength(20)] string Code,
    [Required, MaxLength(120)] string Name,
    int SequenceNo,
    bool IsActive);

// Employees
public record EmployeeDto(
    Guid Id, int Number, string Code, string Name, string? Mobile, string? Department, string? Designation,
    Guid? PlantId, string? PlantName, Guid? ProcessId, string? ProcessName,
    bool IsActive, DateTime CreatedAtUtc);

public record EmployeeUpsertRequest(
    [Required, MaxLength(20)] string Code,
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
    [Required, MaxLength(20)] string Code,
    [Required, MaxLength(120)] string Name,
    [MaxLength(120)] string? ContactPerson,
    [MaxLength(30)] string? Mobile,
    [EmailAddress, MaxLength(150)] string? Email,
    [MaxLength(250)] string? Address,
    bool IsActive);

// Roles
public record RoleDto(
    Guid Id, int Number, string Name, string? Description,
    bool CanView, bool CanAdd, bool CanEdit, bool CanDelete, bool CanViewReports,
    bool IsActive, DateTime CreatedAtUtc);

public record RoleUpsertRequest(
    [Required, MaxLength(60)] string Name,
    [MaxLength(250)] string? Description,
    bool CanView,
    bool CanAdd,
    bool CanEdit,
    bool CanDelete,
    bool CanViewReports,
    bool IsActive);

// Shopfloors
public record ShopfloorDto(
    Guid Id, int Number, string Code, string Name, int SequenceNo, bool IsStorage,
    string BatchMode,
    Guid? ProcessId, string? ProcessName,
    int SheetCount,
    bool IsActive, DateTime CreatedAtUtc);

public record ShopfloorUpsertRequest(
    [Required, MaxLength(20)] string Code,
    [Required, MaxLength(80)] string Name,
    int SequenceNo,
    bool IsStorage,
    [Required, MaxLength(20)] string BatchMode,
    Guid? ProcessId,
    bool IsActive);

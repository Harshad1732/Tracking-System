using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

public record ClientLogRequest(
    [Required, MaxLength(20)]  string Level,
    [Required, MaxLength(2000)] string Message,
    [MaxLength(10)]   string? Method,
    [MaxLength(500)]  string? Path,
    int? StatusCode,
    [MaxLength(200)]  string? ExceptionType,
    string? StackTrace,
    string? RequestBody,
    string? ResponseBody,
    string? ClientContext);

public record ApplicationLogDto(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    string Source,
    string Level,
    string? Method,
    string? Path,
    int? StatusCode,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? RequestBody,
    string? ResponseBody,
    string? UserAgent,
    string? IpAddress,
    string? ClientContext,
    DateTime CreatedAtUtc);

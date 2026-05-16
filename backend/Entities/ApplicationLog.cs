using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// One row per server-side exception, failed HTTP response (>=400), or client-reported
/// error. Used by support to diagnose problems without asking the user "what did you do?".
/// </summary>
public class ApplicationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null for pre-auth failures (e.g. failed login attempts).</summary>
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }

    [Required, MaxLength(20)]
    public string Source { get; set; } = "backend"; // "backend" | "frontend"

    [Required, MaxLength(20)]
    public string Level { get; set; } = "Error";    // "Error" | "Warning" | "Info"

    [MaxLength(10)]
    public string? Method { get; set; }

    [MaxLength(500)]
    public string? Path { get; set; }

    public int? StatusCode { get; set; }

    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ExceptionType { get; set; }

    public string? StackTrace { get; set; }

    /// <summary>Up to 4 KB of the request body, with sensitive fields scrubbed.</summary>
    public string? RequestBody { get; set; }

    /// <summary>Up to 4 KB of the response body.</summary>
    public string? ResponseBody { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    /// <summary>Free-form JSON of extra client-supplied context (route, component, etc.).</summary>
    public string? ClientContext { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

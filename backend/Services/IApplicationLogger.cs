using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

public record LogEntryInput(
    string Level,
    string Source,
    string Message,
    string? Method = null,
    string? Path = null,
    int? StatusCode = null,
    string? ExceptionType = null,
    string? StackTrace = null,
    string? RequestBody = null,
    string? ResponseBody = null,
    string? UserAgent = null,
    string? IpAddress = null,
    string? ClientContext = null,
    Guid? TenantId = null,
    Guid? UserId = null);

public interface IApplicationLogger
{
    Task WriteAsync(LogEntryInput entry, CancellationToken ct = default);
}

public class ApplicationLogger : IApplicationLogger
{
    private const int MaxBody = 4000;     // about 4 KB — enough for a typical request body
    private const int MaxStack = 8000;    // a stack trace can be long but not unbounded

    private readonly AppDbContext _db;
    private readonly ILogger<ApplicationLogger> _console;

    public ApplicationLogger(AppDbContext db, ILogger<ApplicationLogger> console)
    {
        _db = db;
        _console = console;
    }

    public async Task WriteAsync(LogEntryInput e, CancellationToken ct = default)
    {
        try
        {
            var row = new ApplicationLog
            {
                Level         = Truncate(e.Level, 20)        ?? "Error",
                Source        = Truncate(e.Source, 20)       ?? "backend",
                Message       = Truncate(e.Message, 2000)    ?? string.Empty,
                Method        = Truncate(e.Method, 10),
                Path          = Truncate(e.Path, 500),
                StatusCode    = e.StatusCode,
                ExceptionType = Truncate(e.ExceptionType, 200),
                StackTrace    = Truncate(e.StackTrace, MaxStack),
                RequestBody   = Truncate(e.RequestBody, MaxBody),
                ResponseBody  = Truncate(e.ResponseBody, MaxBody),
                UserAgent     = Truncate(e.UserAgent, 500),
                IpAddress     = Truncate(e.IpAddress, 64),
                ClientContext = Truncate(e.ClientContext, MaxBody),
                TenantId      = e.TenantId,
                UserId        = e.UserId
            };
            _db.ApplicationLogs.Add(row);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception logEx)
        {
            // Last-resort fallback: write to the standard ILogger so we don't lose the
            // original event if persisting it fails (e.g., DB is down).
            _console.LogError(logEx, "Failed to persist application log entry: {Message}", e.Message);
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }

    public static Guid? ParseGuid(string? raw)
        => Guid.TryParse(raw, out var g) ? g : (Guid?)null;
}

/// <summary>Convenience helpers for extracting tenant/user from the current request.</summary>
public static class LoggingClaimExtensions
{
    public static Guid? CurrentTenantId(this ClaimsPrincipal? p) =>
        ApplicationLogger.ParseGuid(p?.FindFirst(TrackerClaims.TenantId)?.Value);

    public static Guid? CurrentUserId(this ClaimsPrincipal? p) =>
        ApplicationLogger.ParseGuid(p?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
}

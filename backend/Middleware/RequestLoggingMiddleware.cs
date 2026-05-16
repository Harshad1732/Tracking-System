using System.Text;
using System.Text.RegularExpressions;
using Tracker.Services;

namespace Tracker.Middleware;

/// <summary>
/// Catches every unhandled exception and every HTTP failure response (status &gt;= 400) and
/// persists a rich log entry — method, path, request body, user, tenant, stack trace.
/// Successful 2xx/3xx responses are NOT logged here (they'd flood the table); use ILogger
/// for those.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Endpoints we never log a body for — they contain secrets.
    private static readonly string[] SkipBodyPaths =
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh",
        "/api/auth/reset-password",
        "/api/auth/forgot-password",
        "/api/users/me/reset-password"
    };

    // Don't recursively log the client-log ingestion endpoint.
    private const string ClientLogPath = "/api/logs/client";

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(ClientLogPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Buffer the request body so we can re-read it for logging after the controller
        // has already consumed it.
        context.Request.EnableBuffering();
        var requestBody = await PeekRequestBodyAsync(context);

        Exception? caught = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caught = ex;
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"error\":\"An unexpected server error occurred.\"}");
            }
        }

        var status = context.Response.StatusCode;
        if (caught is null && status < 400) return;

        var loggable = ShouldLogBody(context.Request.Path) ? requestBody : "[redacted]";
        var appLog = context.RequestServices.GetRequiredService<IApplicationLogger>();
        await appLog.WriteAsync(new LogEntryInput(
            Level:         caught is not null || status >= 500 ? "Error" : "Warning",
            Source:        "backend",
            Message:       caught?.Message ?? $"HTTP {status} {context.Request.Method} {context.Request.Path}",
            Method:        context.Request.Method,
            Path:          context.Request.Path,
            StatusCode:    status,
            ExceptionType: caught?.GetType().FullName,
            StackTrace:    caught?.ToString(),
            RequestBody:   loggable,
            ResponseBody:  null,
            UserAgent:     context.Request.Headers.UserAgent.ToString(),
            IpAddress:     context.Connection.RemoteIpAddress?.ToString(),
            TenantId:      context.User.CurrentTenantId(),
            UserId:        context.User.CurrentUserId()
        ), context.RequestAborted);

        if (caught is not null)
            _logger.LogError(caught, "Unhandled exception in {Method} {Path}", context.Request.Method, context.Request.Path);
        else if (status >= 500)
            _logger.LogError("Failure {Status} in {Method} {Path}", status, context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Client error {Status} in {Method} {Path}", status, context.Request.Method, context.Request.Path);
    }

    private static bool ShouldLogBody(PathString path)
    {
        foreach (var skip in SkipBodyPaths)
            if (path.StartsWithSegments(skip, StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }

    private static async Task<string?> PeekRequestBodyAsync(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is null or 0) return null;
        if (ctx.Request.ContentLength > 64 * 1024) return "[body > 64KB, truncated]";

        ctx.Request.Body.Position = 0;
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;
        return Scrub(raw);
    }

    // Replace common secret fields with [redacted] before the body hits the DB.
    private static readonly Regex SecretFieldRegex = new(
        "\"(password|newPassword|currentPassword|accessToken|refreshToken|idToken|token)\"\\s*:\\s*\"[^\"]*\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string Scrub(string raw)
        => SecretFieldRegex.Replace(raw, m =>
            $"\"{m.Groups[1].Value}\":\"[redacted]\"");
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Filters;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IApplicationLogger _logger;
    public LogsController(AppDbContext db, IApplicationLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // Anonymous: an unauthenticated page (login, sign-up, landing) can still report errors.
    // We do NOT include the IP-or-user-derived tenant on anonymous calls.
    [HttpPost("client")]
    [AllowAnonymous]
    public async Task<IActionResult> ClientLog([FromBody] ClientLogRequest req, CancellationToken ct)
    {
        await _logger.WriteAsync(new LogEntryInput(
            Level:         req.Level,
            Source:        "frontend",
            Message:       req.Message,
            Method:        req.Method,
            Path:          req.Path,
            StatusCode:    req.StatusCode,
            ExceptionType: req.ExceptionType,
            StackTrace:    req.StackTrace,
            RequestBody:   req.RequestBody,
            ResponseBody:  req.ResponseBody,
            UserAgent:     HttpContext.Request.Headers.UserAgent.ToString(),
            IpAddress:     HttpContext.Connection.RemoteIpAddress?.ToString(),
            ClientContext: req.ClientContext,
            TenantId:      User.CurrentTenantId(),
            UserId:        User.CurrentUserId()
        ), ct);
        return NoContent();
    }

    // Admin-only viewer. Filters by source/level/date range; paginated to avoid
    // dumping millions of rows.
    [HttpGet]
    [RequirePermission(Resources.Workspace, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<ApplicationLogDto>>> List(
        [FromQuery] string? source,
        [FromQuery] string? level,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        if (take is < 1 or > 1000) take = 200;

        var q = _db.ApplicationLogs.AsNoTracking()
            .Where(l => l.TenantId == TenantId || l.TenantId == null);

        if (!string.IsNullOrWhiteSpace(source)) q = q.Where(l => l.Source == source);
        if (!string.IsNullOrWhiteSpace(level))  q = q.Where(l => l.Level == level);
        if (from is not null) q = q.Where(l => l.CreatedAtUtc >= from);
        if (to   is not null) q = q.Where(l => l.CreatedAtUtc <= to);

        var rows = await q
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(take)
            .Select(l => new ApplicationLogDto(
                l.Id, l.TenantId, l.UserId, l.Source, l.Level,
                l.Method, l.Path, l.StatusCode, l.Message,
                l.ExceptionType, l.StackTrace, l.RequestBody, l.ResponseBody,
                l.UserAgent, l.IpAddress, l.ClientContext, l.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(rows);
    }
}

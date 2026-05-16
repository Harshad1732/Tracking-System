using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Services;

namespace Tracker.Controllers;

/// <summary>
/// Cross-tenant operations available only to platform admins (users with
/// <see cref="Entities.User.IsPlatformAdmin"/> = true). Every endpoint here ignores
/// the caller's `tid` claim — they're meant to operate ACROSS tenants.
/// </summary>
[ApiController]
[Authorize]
[Route("api/platform")]
public class PlatformController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthService _auth;
    public PlatformController(AppDbContext db, IAuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    // Lists every tenant on the platform with summary counts.
    [HttpGet("tenants")]
    public async Task<ActionResult<IReadOnlyList<PlatformTenantDto>>> ListTenants(CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();

        var rows = await _db.Tenants.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new PlatformTenantDto(
                t.Id, t.Name, t.Slug, t.IsActive,
                _db.Users.Count(u => u.TenantId == t.Id),
                _db.Shopfloors.Count(s => s.TenantId == t.Id),
                _db.GlassSheets.Count(g => g.TenantId == t.Id),
                t.Subscription != null ? t.Subscription.Plan.Code : null,
                t.Subscription != null ? t.Subscription.Status : null,
                t.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(rows);
    }

    // Issues a fresh JWT bound to the target tenant. The user record stays in the
    // platform admin's home tenant — the JWT's `tid` is what scopes data queries.
    [HttpPost("switch/{tenantId:guid}")]
    public async Task<ActionResult<AuthResponse>> SwitchTenant(Guid tenantId, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();

        var target = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct);
        if (target is null) return NotFound(new { error = "Tenant not found or inactive." });

        var idRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idRaw, out var userId)) return Unauthorized();
        var me = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (me is null) return Forbid();
        var stillPlatformAdmin = await _db.PlatformAdmins.AnyAsync(pa => pa.UserId == userId, ct);
        if (!stillPlatformAdmin) return Forbid();

        var result = await _auth.IssueTokensForTenantAsync(me, target, ct);
        return Ok(result);
    }

    // Enable/disable a tenant. Disabled tenants block all logins for their users.
    [HttpPost("tenants/{tenantId:guid}/active")]
    public async Task<IActionResult> SetTenantActive(Guid tenantId, [FromBody] SetTenantActiveRequest req, CancellationToken ct)
    {
        if (!IsPlatformAdmin()) return Forbid();

        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t is null) return NotFound();
        t.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private bool IsPlatformAdmin() =>
        User.FindFirst(TrackerClaims.PlatformAdmin)?.Value == "true";
}

public record SetTenantActiveRequest(bool IsActive);

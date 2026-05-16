using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;

namespace Tracker.Services;

/// <summary>One (resource, action) tuple the caller holds.</summary>
public record GrantedPermission(string Resource, string Action);

/// <summary>
/// The full set of permissions the current request has, resolved by unioning every
/// matching role assignment the user holds.
/// </summary>
public class EffectivePermissions
{
    public bool IsPlatformAdmin { get; init; }
    public bool IsSystemAdmin { get; init; }
    public IReadOnlyList<GrantedPermission> Grants { get; init; } = Array.Empty<GrantedPermission>();
    public IReadOnlyList<string> RoleNames { get; init; } = Array.Empty<string>();

    public bool Has(string resource, string action)
    {
        if (IsPlatformAdmin || IsSystemAdmin) return true;
        foreach (var g in Grants)
        {
            if (string.Equals(g.Resource, resource, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(g.Action, action, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static readonly EffectivePermissions None = new();
    public static readonly EffectivePermissions Platform = new() { IsPlatformAdmin = true };
}

public interface IPermissionService
{
    Task<EffectivePermissions> GetForCurrentRequestAsync(CancellationToken ct = default);
    Task<bool> HasAsync(string resource, string action, CancellationToken ct = default);

    /// <summary>Resolves permissions for a specific user in a specific tenant+plant context.
    /// Used by AuthService during login/refresh when there is no HttpContext claims yet.</summary>
    Task<EffectivePermissions> ResolveAsync(Guid userId, Guid tenantId, Guid? plantId, CancellationToken ct = default);
}

public class PermissionService : IPermissionService
{
    private const string CacheKey = "tracker.permissions.cached";

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public PermissionService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task<bool> HasAsync(string resource, string action, CancellationToken ct = default)
    {
        var eff = await GetForCurrentRequestAsync(ct);
        return eff.Has(resource, action);
    }

    public async Task<EffectivePermissions> GetForCurrentRequestAsync(CancellationToken ct = default)
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return EffectivePermissions.None;

        if (ctx.Items.TryGetValue(CacheKey, out var cached) && cached is EffectivePermissions cset)
            return cset;

        var principal = ctx.User;
        if (principal?.Identity?.IsAuthenticated != true) return EffectivePermissions.None;

        // Platform admin claim short-circuits all DB lookups. The claim is signed in the JWT.
        if (principal.FindFirst(TrackerClaims.PlatformAdmin)?.Value == "true")
        {
            var p = EffectivePermissions.Platform;
            ctx.Items[CacheKey] = p;
            return p;
        }

        var idRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idRaw, out var userId)) return EffectivePermissions.None;

        var tidRaw = principal.FindFirstValue(TrackerClaims.TenantId);
        if (!Guid.TryParse(tidRaw, out var tenantId)) return EffectivePermissions.None;

        Guid? plantId = null;
        var pidRaw = principal.FindFirstValue(TrackerClaims.PlantId);
        if (Guid.TryParse(pidRaw, out var pid)) plantId = pid;

        var set = await ResolveAsync(userId, tenantId, plantId, ct);
        ctx.Items[CacheKey] = set;
        return set;
    }

    public async Task<EffectivePermissions> ResolveAsync(
        Guid userId, Guid tenantId, Guid? plantId, CancellationToken ct = default)
    {
        // Platform admin lookup — by-pass tenant scope entirely.
        if (await _db.PlatformAdmins.AnyAsync(pa => pa.UserId == userId, ct))
            return EffectivePermissions.Platform;

        var userExists = await _db.Users.AnyAsync(
            u => u.Id == userId && u.TenantId == tenantId && u.IsActive, ct);
        if (!userExists) return EffectivePermissions.None;

        // Pull every assignment that matches the current scope: Tenant-scoped always
        // applies; Plant-scoped applies only when ScopeId equals the current plant.
        var assignments = await _db.UserRoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.TenantId == tenantId)
            .Where(a => a.ScopeType == ScopeTypes.Tenant
                     || (a.ScopeType == ScopeTypes.Plant && a.ScopeId == plantId))
            .Select(a => new { a.RoleId, a.Role.Name, a.Role.IsSystemAdmin, a.Role.IsActive })
            .ToListAsync(ct);

        var activeRoles = assignments.Where(a => a.IsActive).ToList();
        if (activeRoles.Count == 0)
            return EffectivePermissions.None;

        if (activeRoles.Any(a => a.IsSystemAdmin))
        {
            return new EffectivePermissions
            {
                IsSystemAdmin = true,
                RoleNames = activeRoles.Select(a => a.Name).Distinct().ToList()
            };
        }

        var roleIds = activeRoles.Select(a => a.RoleId).Distinct().ToList();

        var grants = await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new GrantedPermission(
                rp.Permission.Resource.Code,
                rp.Permission.Action.Code))
            .Distinct()
            .ToListAsync(ct);

        return new EffectivePermissions
        {
            Grants = grants,
            RoleNames = activeRoles.Select(a => a.Name).Distinct().ToList()
        };
    }
}

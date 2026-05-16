using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Filters;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Authorize]
[Route("api/roles")]
public class RolesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRoleAdminService _admin;

    public RolesController(AppDbContext db, IRoleAdminService admin)
    {
        _db = db;
        _admin = admin;
    }

    private Guid? ActorUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;

    [HttpGet]
    [RequirePermission(Resources.Roles, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct)
    {
        var roles = await _db.RoleDefinitions
            .AsNoTracking()
            .Where(r => r.TenantId == TenantId)
            .OrderBy(r => r.Number)
            .ToListAsync(ct);

        // One query for assigned-user counts, one for permissions — cheaper than N+1.
        var roleIds = roles.Select(r => r.Id).ToList();
        var counts = await _db.UserRoleAssignments
            .Where(a => roleIds.Contains(a.RoleId))
            .GroupBy(a => a.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Select(x => x.UserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        var perms = await _db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new
            {
                rp.RoleId,
                Resource = rp.Permission.Resource.Code,
                Action = rp.Permission.Action.Code
            })
            .ToListAsync(ct);
        var permsByRole = perms.GroupBy(p => p.RoleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RolePermissionDto>)g
                .Select(x => new RolePermissionDto(x.Resource, x.Action)).ToList());

        // System admin holds every permission implicitly — expand to the full catalog
        // for display, so the matrix UI shows all boxes ticked.
        IReadOnlyList<RolePermissionDto>? allPerms = null;
        async Task<IReadOnlyList<RolePermissionDto>> AllPermsAsync()
        {
            return allPerms ??= await _db.Permissions
                .AsNoTracking()
                .Select(p => new RolePermissionDto(p.Resource.Code, p.Action.Code))
                .ToListAsync(ct);
        }

        var result = new List<RoleDto>();
        foreach (var r in roles)
        {
            var rolePerms = r.IsSystemAdmin
                ? await AllPermsAsync()
                : permsByRole.GetValueOrDefault(r.Id, Array.Empty<RolePermissionDto>());

            result.Add(new RoleDto(
                r.Id, r.Number, r.Name, r.Description,
                r.IsSystem, r.IsSystemAdmin, r.IsActive,
                rolePerms,
                counts.GetValueOrDefault(r.Id, 0),
                r.CreatedAtUtc));
        }
        return Ok(result);
    }

    [HttpGet("catalog")]
    [RequirePermission(Resources.Roles, Actions.View)]
    public async Task<ActionResult<PermissionCatalogDto>> Catalog(CancellationToken ct)
    {
        var resources = await _db.PermResources.AsNoTracking()
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new PermResourceDto(r.Id, r.Code, r.Name, r.Description, r.SortOrder, r.IsSystem))
            .ToListAsync(ct);
        var actions = await _db.PermActions.AsNoTracking()
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .Select(a => new PermActionDto(a.Id, a.Code, a.Name, a.SortOrder, a.IsSystem))
            .ToListAsync(ct);
        return Ok(new PermissionCatalogDto(resources, actions));
    }

    [HttpPost]
    [RequirePermission(Resources.Roles, Actions.Add)]
    public async Task<ActionResult<RoleDto>> Create(RoleUpsertRequest req, CancellationToken ct)
    {
        var result = await _admin.CreateAsync(
            TenantId, req.Name, req.Description,
            (req.Permissions ?? Array.Empty<RolePermissionDto>())
                .Select(p => (p.Resource, p.Action)).ToList(),
            req.IsActive, ActorUserId, ct);

        if (!result.Ok) return Conflict(new { error = result.Error });
        return await SingleAsync(result.Role!.Id, ct);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Resources.Roles, Actions.Edit)]
    public async Task<ActionResult<RoleDto>> Update(Guid id, RoleUpsertRequest req, CancellationToken ct)
    {
        var result = await _admin.UpdateAsync(
            TenantId, id, req.Name, req.Description,
            (req.Permissions ?? Array.Empty<RolePermissionDto>())
                .Select(p => (p.Resource, p.Action)).ToList(),
            req.IsActive, ActorUserId, ct);

        if (!result.Ok) return BadRequest(new { error = result.Error });
        return await SingleAsync(id, ct);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Roles, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _admin.DeleteAsync(TenantId, id, ActorUserId, ct);
        if (!result.Ok) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    private async Task<ActionResult<RoleDto>> SingleAsync(Guid id, CancellationToken ct)
    {
        // Reuse the same shape as List() — fetch the single role with its perms + count.
        var r = await _db.RoleDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct);
        if (r is null) return NotFound();

        var count = await _db.UserRoleAssignments
            .Where(a => a.RoleId == id)
            .Select(a => a.UserId).Distinct().CountAsync(ct);

        IReadOnlyList<RolePermissionDto> perms = r.IsSystemAdmin
            ? await _db.Permissions.AsNoTracking()
                .Select(p => new RolePermissionDto(p.Resource.Code, p.Action.Code))
                .ToListAsync(ct)
            : await _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == id)
                .Select(rp => new RolePermissionDto(rp.Permission.Resource.Code, rp.Permission.Action.Code))
                .ToListAsync(ct);

        return Ok(new RoleDto(
            r.Id, r.Number, r.Name, r.Description,
            r.IsSystem, r.IsSystemAdmin, r.IsActive,
            perms, count, r.CreatedAtUtc));
    }
}

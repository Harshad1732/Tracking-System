using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
using Tracker.Filters;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IPlanLimitService _limits;
    private readonly INumberGenerator _ng;
    private readonly IPermissionService _perms;
    private readonly IUserRoleService _userRoles;

    public UsersController(
        AppDbContext db, IPasswordHasher hasher, IPlanLimitService limits,
        INumberGenerator ng, IPermissionService perms, IUserRoleService userRoles)
    {
        _db = db;
        _hasher = hasher;
        _limits = limits;
        _ng = ng;
        _perms = perms;
        _userRoles = userRoles;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null) return NotFound();

        var eff = await _perms.GetForCurrentRequestAsync(ct);
        var isPlatformAdmin = await _db.PlatformAdmins.AnyAsync(pa => pa.UserId == userId, ct);

        return new UserDto(
            user.Id, user.Email, user.FullName,
            eff.RoleNames,
            eff.IsSystemAdmin,
            isPlatformAdmin,
            eff.Grants.Select(g => new PermissionGrantDto(g.Resource, g.Action)).ToList(),
            user.PlantId,
            PlantId);
    }

    [HttpGet]
    [RequirePermission(Resources.Users, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<UserAdminDto>>> List(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();

        var assignmentsRaw = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.TenantId == TenantId)
            .Select(a => new
            {
                a.Id, a.UserId, a.RoleId, a.ScopeType, a.ScopeId,
                RoleName = a.Role.Name,
                IsSystemAdmin = a.Role.IsSystemAdmin
            })
            .ToListAsync(ct);

        var plantNames = await _db.Plants.AsNoTracking()
            .Where(p => p.TenantId == TenantId)
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var platformAdmins = (await _db.PlatformAdmins.AsNoTracking()
            .Where(pa => userIds.Contains(pa.UserId))
            .Select(pa => pa.UserId).ToListAsync(ct))
            .ToHashSet();

        var assignmentsByUser = assignmentsRaw
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => new UserAssignmentDto(
                a.Id, a.RoleId, a.RoleName, a.IsSystemAdmin,
                a.ScopeType, a.ScopeId,
                a.ScopeId.HasValue && plantNames.TryGetValue(a.ScopeId.Value, out var n) ? n : null
            )).ToList());

        var result = users.Select(u => new UserAdminDto(
            u.Id, u.Number, u.Email, u.FullName,
            u.Provider, u.IsActive, u.PasswordHash != null,
            u.PlantId, u.PlantId.HasValue && plantNames.TryGetValue(u.PlantId.Value, out var dn) ? dn : null,
            platformAdmins.Contains(u.Id),
            assignmentsByUser.GetValueOrDefault(u.Id, new List<UserAssignmentDto>()),
            u.CreatedAtUtc
        )).ToList();

        return Ok((IReadOnlyList<UserAdminDto>)result);
    }

    [HttpPost]
    [RequirePermission(Resources.Users, Actions.Add)]
    public async Task<ActionResult<UserAdminDto>> Create(CreateUserRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.TenantId == TenantId && u.Email == email, ct))
            return Conflict(new { error = "A user with this email already exists in this workspace." });

        var limit = await _limits.CheckUsersAsync(TenantId, 1, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });

        if (req.DefaultPlantId is Guid pid &&
            !await _db.Plants.AnyAsync(p => p.Id == pid && p.TenantId == TenantId, ct))
        {
            return BadRequest(new { error = "Plant not found." });
        }

        var user = new User
        {
            TenantId = TenantId,
            Number = await _ng.NextUserAsync(TenantId, ct),
            Email = email,
            FullName = req.FullName,
            PasswordHash = _hasher.Hash(req.Password),
            PlantId = req.DefaultPlantId,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var assignments = (req.Assignments ?? Array.Empty<AssignmentInputDto>())
            .Select(a => new AssignmentInput(a.RoleId, a.ScopeType, a.ScopeId))
            .ToList();
        if (assignments.Count > 0)
        {
            var res = await _userRoles.ReplaceAsync(TenantId, user.Id, assignments, CurrentUserId, ct);
            if (!res.Ok)
            {
                // Roll back the user — assignment was invalid.
                _db.Users.Remove(user);
                await _db.SaveChangesAsync(ct);
                return BadRequest(new { error = res.Error });
            }
        }

        return await SingleAsync(user.Id, ct);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Resources.Users, Actions.Edit)]
    public async Task<ActionResult<UserAdminDto>> Update(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();

        var isSelf = user.Id == CurrentUserId;

        // Self-protection: never let the actor lock themselves out by changing their own
        // assignments away from admin, OR deactivating themselves.
        if (isSelf && !req.IsActive)
            return BadRequest(new { error = "You can't deactivate your own account." });

        if (req.DefaultPlantId is Guid pid &&
            !await _db.Plants.AnyAsync(p => p.Id == pid && p.TenantId == TenantId, ct))
        {
            return BadRequest(new { error = "Plant not found." });
        }

        // Last-admin guard — if this user currently holds tenant-scoped admin and the
        // new assignments wouldn't include it, ensure someone else still does.
        var currentlyAdmin = await _db.UserRoleAssignments.AnyAsync(a =>
            a.UserId == user.Id && a.TenantId == TenantId &&
            a.ScopeType == ScopeTypes.Tenant &&
            a.Role.IsSystemAdmin && a.Role.IsActive, ct);

        var willBeAdmin = false;
        if (req.Assignments is { Count: > 0 })
        {
            var adminIds = await _db.RoleDefinitions
                .Where(r => r.TenantId == TenantId && r.IsSystemAdmin && r.IsActive)
                .Select(r => r.Id).ToListAsync(ct);
            willBeAdmin = req.Assignments.Any(a =>
                adminIds.Contains(a.RoleId) && a.ScopeType == ScopeTypes.Tenant);
        }

        if (currentlyAdmin && (!willBeAdmin || !req.IsActive))
        {
            var anotherAdmin = await _userRoles.AnotherAdminExistsAsync(TenantId, user.Id, ct);
            if (!anotherAdmin)
                return BadRequest(new { error = "At least one active workspace admin is required." });
        }

        user.FullName = req.FullName;
        user.IsActive = req.IsActive;
        user.PlantId = req.DefaultPlantId;
        await _db.SaveChangesAsync(ct);

        var assignments = (req.Assignments ?? Array.Empty<AssignmentInputDto>())
            .Select(a => new AssignmentInput(a.RoleId, a.ScopeType, a.ScopeId))
            .ToList();
        var res = await _userRoles.ReplaceAsync(TenantId, user.Id, assignments, CurrentUserId, ct);
        if (!res.Ok) return BadRequest(new { error = res.Error });

        return await SingleAsync(user.Id, ct);
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(Resources.Users, Actions.Edit)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetUserPasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();
        user.PasswordHash = _hasher.Hash(req.NewPassword);

        var refresh = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in refresh) t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Users, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();
        if (user.Id == CurrentUserId)
            return BadRequest(new { error = "You can't delete your own account." });

        var currentlyAdmin = await _db.UserRoleAssignments.AnyAsync(a =>
            a.UserId == user.Id && a.TenantId == TenantId &&
            a.ScopeType == ScopeTypes.Tenant &&
            a.Role.IsSystemAdmin && a.Role.IsActive, ct);
        if (currentlyAdmin)
        {
            var anotherAdmin = await _userRoles.AnotherAdminExistsAsync(TenantId, user.Id, ct);
            if (!anotherAdmin)
                return BadRequest(new { error = "At least one active workspace admin is required." });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<UserAdminDto>> SingleAsync(Guid id, CancellationToken ct)
    {
        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct);
        if (u is null) return NotFound();

        var plantNames = await _db.Plants.AsNoTracking()
            .Where(p => p.TenantId == TenantId)
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var assignments = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == id && a.TenantId == TenantId)
            .Select(a => new UserAssignmentDto(
                a.Id, a.RoleId, a.Role.Name, a.Role.IsSystemAdmin,
                a.ScopeType, a.ScopeId, null))
            .ToListAsync(ct);

        var withScopeNames = assignments.Select(a => a with
        {
            ScopeName = a.ScopeId.HasValue && plantNames.TryGetValue(a.ScopeId.Value, out var n) ? n : null
        }).ToList();

        var isPlatformAdmin = await _db.PlatformAdmins.AnyAsync(pa => pa.UserId == id, ct);

        return Ok(new UserAdminDto(
            u.Id, u.Number, u.Email, u.FullName,
            u.Provider, u.IsActive, u.PasswordHash != null,
            u.PlantId, u.PlantId.HasValue && plantNames.TryGetValue(u.PlantId.Value, out var dn) ? dn : null,
            isPlatformAdmin,
            withScopeNames,
            u.CreatedAtUtc));
    }
}

[ApiController]
[Authorize]
[Route("api/workspace")]
public class WorkspaceController : TenantControllerBase
{
    private readonly AppDbContext _db;
    public WorkspaceController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<WorkspaceDto>> Get(CancellationToken ct)
    {
        var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == TenantId, ct);
        if (t is null) return NotFound();
        var users = await _db.Users.CountAsync(u => u.TenantId == TenantId, ct);
        var floors = await _db.Shopfloors.CountAsync(s => s.TenantId == TenantId, ct);
        var plants = await _db.Plants.CountAsync(p => p.TenantId == TenantId, ct);
        return Ok(new WorkspaceDto(t.Id, t.Name, t.Slug, t.CreatedAtUtc, users, floors, plants));
    }

    [HttpPut]
    [RequirePermission(Resources.Workspace, Actions.Edit)]
    public async Task<ActionResult<WorkspaceDto>> Update(UpdateWorkspaceRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == TenantId, ct);
        if (t is null) return NotFound();
        t.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);
        return await Get(ct);
    }
}

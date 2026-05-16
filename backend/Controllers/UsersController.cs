using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : TenantControllerBase
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "Supervisor", "Operator", "Viewer", "User" };

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IPlanLimitService _limits;
    private readonly INumberGenerator _ng;

    public UsersController(AppDbContext db, IPasswordHasher hasher, IPlanLimitService limits, INumberGenerator ng)
    {
        _db = db;
        _hasher = hasher;
        _limits = limits;
        _ng = ng;
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
        return new UserDto(user.Id, user.Email, user.FullName, user.Role);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<UserAdminDto>>> List(CancellationToken ct)
    {
        var items = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId)
            .OrderBy(u => u.Email)
            .Select(u => new UserAdminDto(
                u.Id, u.Number, u.Email, u.FullName, u.Role, u.Provider, u.IsActive,
                u.PasswordHash != null, u.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserAdminDto>> Create(CreateUserRequest req, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(req.Role))
            return BadRequest(new { error = $"Unknown role '{req.Role}'." });

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.TenantId == TenantId && u.Email == email, ct))
            return Conflict(new { error = "A user with this email already exists in this workspace." });

        var limit = await _limits.CheckUsersAsync(TenantId, 1, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });

        var user = new User
        {
            TenantId = TenantId,
            Number = await _ng.NextUserAsync(TenantId, ct),
            Email = email,
            FullName = req.FullName,
            PasswordHash = _hasher.Hash(req.Password),
            Role = req.Role,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return await ToDto(user.Id, ct);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserAdminDto>> Update(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(req.Role))
            return BadRequest(new { error = $"Unknown role '{req.Role}'." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();

        // Don't let admins demote or deactivate themselves and lock out their own workspace.
        if (user.Id == CurrentUserId)
        {
            if (!string.Equals(req.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "You can't change your own role." });
            if (!req.IsActive)
                return BadRequest(new { error = "You can't deactivate your own account." });
        }

        // Prevent demoting the last active Admin.
        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            && (!string.Equals(req.Role, "Admin", StringComparison.OrdinalIgnoreCase) || !req.IsActive))
        {
            var otherActiveAdmins = await _db.Users.CountAsync(u =>
                u.TenantId == TenantId && u.Id != user.Id && u.IsActive && u.Role == "Admin", ct);
            if (otherActiveAdmins == 0)
                return BadRequest(new { error = "At least one active Admin is required." });
        }

        user.FullName = req.FullName;
        user.Role = req.Role;
        user.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await ToDto(user.Id, ct);
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetUserPasswordRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();
        user.PasswordHash = _hasher.Hash(req.NewPassword);

        // Revoke active refresh tokens so the user must sign in again.
        var refresh = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in refresh) t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();
        if (user.Id == CurrentUserId)
            return BadRequest(new { error = "You can't delete your own account." });

        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var otherActiveAdmins = await _db.Users.CountAsync(u =>
                u.TenantId == TenantId && u.Id != user.Id && u.IsActive && u.Role == "Admin", ct);
            if (otherActiveAdmins == 0)
                return BadRequest(new { error = "At least one active Admin is required." });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<UserAdminDto>> ToDto(Guid id, CancellationToken ct)
    {
        var dto = await _db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserAdminDto(
                u.Id, u.Number, u.Email, u.FullName, u.Role, u.Provider, u.IsActive,
                u.PasswordHash != null, u.CreatedAtUtc))
            .FirstAsync(ct);
        return Ok(dto);
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<WorkspaceDto>> Update(UpdateWorkspaceRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == TenantId, ct);
        if (t is null) return NotFound();
        t.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);
        return await Get(ct);
    }
}

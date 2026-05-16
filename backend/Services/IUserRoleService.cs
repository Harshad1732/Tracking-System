using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

public record AssignmentInput(Guid RoleId, string ScopeType, Guid? ScopeId);

public record UserRoleResult(bool Ok, string? Error = null);

public interface IUserRoleService
{
    Task<IReadOnlyList<UserRoleAssignment>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct);

    /// <summary>Replaces the user's full set of assignments with the provided list, atomically.
    /// Empty list = removes everything (caller is responsible for guard checks first).</summary>
    Task<UserRoleResult> ReplaceAsync(
        Guid tenantId, Guid userId, IReadOnlyList<AssignmentInput> assignments,
        Guid? actorUserId, CancellationToken ct);

    /// <summary>True when at least one user other than <paramref name="excludeUserId"/> still
    /// holds an IsSystemAdmin role at tenant scope. Used to block self-lockout.</summary>
    Task<bool> AnotherAdminExistsAsync(Guid tenantId, Guid excludeUserId, CancellationToken ct);
}

public class UserRoleService : IUserRoleService
{
    private readonly AppDbContext _db;

    public UserRoleService(AppDbContext db) => _db = db;

    public Task<IReadOnlyList<UserRoleAssignment>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        _db.UserRoleAssignments
            .AsNoTracking()
            .Include(a => a.Role)
            .Where(a => a.TenantId == tenantId && a.UserId == userId)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<UserRoleAssignment>)t.Result, ct);

    public async Task<UserRoleResult> ReplaceAsync(
        Guid tenantId, Guid userId, IReadOnlyList<AssignmentInput> assignments,
        Guid? actorUserId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        if (user is null) return new(false, "User not found.");

        // Resolve roles (must belong to tenant and be active for the assignment to take effect).
        if (assignments.Count > 0)
        {
            var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();
            var roles = await _db.RoleDefinitions
                .Where(r => r.TenantId == tenantId && roleIds.Contains(r.Id))
                .ToListAsync(ct);
            if (roles.Count != roleIds.Count)
                return new(false, "One or more roles do not exist in this workspace.");

            foreach (var a in assignments)
            {
                if (!ScopeTypes.All.Contains(a.ScopeType))
                    return new(false, $"Unknown scope type '{a.ScopeType}'.");
                if (a.ScopeType == ScopeTypes.Tenant && a.ScopeId is not null)
                    return new(false, "Tenant-scoped assignments must not specify a scope id.");
                if (a.ScopeType == ScopeTypes.Plant)
                {
                    if (a.ScopeId is null) return new(false, "Plant-scoped assignments require a plant id.");
                    var plantOk = await _db.Plants.AnyAsync(
                        p => p.Id == a.ScopeId.Value && p.TenantId == tenantId, ct);
                    if (!plantOk) return new(false, "Plant not found in this workspace.");
                }
            }
        }

        // Dedupe — UNIQUE index on (UserId, RoleId, ScopeType, ScopeId) would reject anyway.
        var deduped = assignments
            .Select(a => new { a.RoleId, a.ScopeType, a.ScopeId })
            .Distinct()
            .ToList();

        var existing = await _db.UserRoleAssignments
            .Where(a => a.UserId == userId && a.TenantId == tenantId)
            .ToListAsync(ct);

        _db.UserRoleAssignments.RemoveRange(existing);

        foreach (var a in deduped)
        {
            _db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = a.RoleId,
                ScopeType = a.ScopeType,
                ScopeId = a.ScopeId,
                CreatedByUserId = actorUserId
            });
        }

        _db.AuthAuditLogs.Add(new AuthAuditLog
        {
            TenantId = tenantId, ActorUserId = actorUserId,
            Action = "user.roles.replace", TargetType = "User", TargetId = userId,
            Details = $"count={deduped.Count}"
        });

        await _db.SaveChangesAsync(ct);
        return new(true);
    }

    public async Task<bool> AnotherAdminExistsAsync(Guid tenantId, Guid excludeUserId, CancellationToken ct) =>
        await _db.UserRoleAssignments.AnyAsync(a =>
            a.TenantId == tenantId &&
            a.UserId != excludeUserId &&
            a.ScopeType == ScopeTypes.Tenant &&
            a.Role.IsSystemAdmin &&
            a.Role.IsActive &&
            a.User.IsActive, ct);
}

using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

public record RoleAdminResult(bool Ok, string? Error = null, RoleDefinition? Role = null);

public interface IRoleAdminService
{
    Task<RoleAdminResult> CreateAsync(Guid tenantId, string name, string? description,
        IReadOnlyList<(string Resource, string Action)> permissions, bool isActive,
        Guid? actorUserId, CancellationToken ct);

    Task<RoleAdminResult> UpdateAsync(Guid tenantId, Guid roleId, string name, string? description,
        IReadOnlyList<(string Resource, string Action)> permissions, bool isActive,
        Guid? actorUserId, CancellationToken ct);

    Task<RoleAdminResult> DeleteAsync(Guid tenantId, Guid roleId, Guid? actorUserId, CancellationToken ct);

    Task<IReadOnlyList<(string Resource, string Action)>> GetPermissionsAsync(
        Guid tenantId, Guid roleId, CancellationToken ct);
}

public class RoleAdminService : IRoleAdminService
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;

    public RoleAdminService(AppDbContext db, INumberGenerator ng)
    {
        _db = db;
        _ng = ng;
    }

    public async Task<IReadOnlyList<(string Resource, string Action)>> GetPermissionsAsync(
        Guid tenantId, Guid roleId, CancellationToken ct)
    {
        var role = await _db.RoleDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);
        if (role is null) return Array.Empty<(string, string)>();

        // System admin implicitly has every permission — return the full catalog.
        if (role.IsSystemAdmin)
        {
            return await _db.Permissions
                .AsNoTracking()
                .Select(p => new ValueTuple<string, string>(p.Resource.Code, p.Action.Code))
                .ToListAsync(ct);
        }

        return await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => new ValueTuple<string, string>(rp.Permission.Resource.Code, rp.Permission.Action.Code))
            .ToListAsync(ct);
    }

    public async Task<RoleAdminResult> CreateAsync(
        Guid tenantId, string name, string? description,
        IReadOnlyList<(string Resource, string Action)> permissions, bool isActive,
        Guid? actorUserId, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return new(false, "Role name is required.");
        if (await _db.RoleDefinitions.AnyAsync(r => r.TenantId == tenantId && r.Name == name, ct))
            return new(false, "A role with this name already exists.");

        var permIds = await ResolvePermissionIdsAsync(permissions, ct);
        if (permIds is null) return new(false, "One or more permissions reference an unknown resource or action.");

        var role = new RoleDefinition
        {
            TenantId = tenantId,
            Number = await _ng.NextRoleAsync(tenantId, ct),
            Name = name,
            Description = description,
            IsActive = isActive,
            IsSystem = false,
            IsSystemAdmin = false
        };
        _db.RoleDefinitions.Add(role);

        foreach (var pid in permIds)
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });

        _db.AuthAuditLogs.Add(new AuthAuditLog
        {
            TenantId = tenantId, ActorUserId = actorUserId,
            Action = "role.create", TargetType = "Role", TargetId = role.Id,
            Details = $"name={name}; perms={permIds.Count}"
        });

        await _db.SaveChangesAsync(ct);
        return new(true, Role: role);
    }

    public async Task<RoleAdminResult> UpdateAsync(
        Guid tenantId, Guid roleId, string name, string? description,
        IReadOnlyList<(string Resource, string Action)> permissions, bool isActive,
        Guid? actorUserId, CancellationToken ct)
    {
        var role = await _db.RoleDefinitions
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);
        if (role is null) return new(false, "Role not found.");

        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return new(false, "Role name is required.");

        if (role.IsSystem && !string.Equals(role.Name, name, StringComparison.Ordinal))
            return new(false, "Built-in roles cannot be renamed.");

        if (role.IsSystemAdmin && !isActive)
            return new(false, "The built-in Admin role cannot be deactivated.");

        if (await _db.RoleDefinitions.AnyAsync(
            r => r.TenantId == tenantId && r.Name == name && r.Id != roleId, ct))
            return new(false, "A role with this name already exists.");

        var permIds = await ResolvePermissionIdsAsync(permissions, ct);
        if (permIds is null) return new(false, "One or more permissions reference an unknown resource or action.");

        role.Name = name;
        role.Description = description;
        role.IsActive = isActive;

        // Replace permissions for non-admin roles. Admin role short-circuits via IsSystemAdmin.
        if (!role.IsSystemAdmin)
        {
            var existing = _db.RolePermissions.Where(rp => rp.RoleId == roleId);
            _db.RolePermissions.RemoveRange(existing);
            foreach (var pid in permIds)
                _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
        }

        _db.AuthAuditLogs.Add(new AuthAuditLog
        {
            TenantId = tenantId, ActorUserId = actorUserId,
            Action = "role.update", TargetType = "Role", TargetId = roleId,
            Details = $"name={name}; perms={permIds.Count}; active={isActive}"
        });

        await _db.SaveChangesAsync(ct);
        return new(true, Role: role);
    }

    public async Task<RoleAdminResult> DeleteAsync(
        Guid tenantId, Guid roleId, Guid? actorUserId, CancellationToken ct)
    {
        var role = await _db.RoleDefinitions
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);
        if (role is null) return new(false, "Role not found.");

        if (role.IsSystem)
            return new(false, "Built-in roles cannot be deleted.");

        var assigned = await _db.UserRoleAssignments.CountAsync(a => a.RoleId == roleId, ct);
        if (assigned > 0)
            return new(false, $"This role is assigned to {assigned} user(s). Reassign them first.");

        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(rp => rp.RoleId == roleId));
        _db.RoleDefinitions.Remove(role);

        _db.AuthAuditLogs.Add(new AuthAuditLog
        {
            TenantId = tenantId, ActorUserId = actorUserId,
            Action = "role.delete", TargetType = "Role", TargetId = roleId,
            Details = $"name={role.Name}"
        });

        await _db.SaveChangesAsync(ct);
        return new(true);
    }

    /// <summary>Looks up Permission ids by (resource, action) string pairs. Returns null if any pair is unknown.</summary>
    private async Task<List<Guid>?> ResolvePermissionIdsAsync(
        IReadOnlyList<(string Resource, string Action)> pairs, CancellationToken ct)
    {
        if (pairs.Count == 0) return new List<Guid>();

        var distinct = pairs
            .Select(p => (p.Resource.Trim(), p.Action.Trim()))
            .Distinct()
            .ToList();

        var lookup = await _db.Permissions
            .AsNoTracking()
            .Select(p => new { p.Id, ResourceCode = p.Resource.Code, ActionCode = p.Action.Code })
            .ToListAsync(ct);

        var dict = lookup.ToDictionary(
            x => (x.ResourceCode, x.ActionCode),
            x => x.Id,
            new TupleStringComparer());

        var ids = new List<Guid>();
        foreach (var (r, a) in distinct)
        {
            if (!dict.TryGetValue((r, a), out var id))
                return null;
            ids.Add(id);
        }
        return ids;
    }

    private sealed class TupleStringComparer : IEqualityComparer<(string, string)>
    {
        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string) obj) =>
            HashCode.Combine(
                obj.Item1.ToLowerInvariant().GetHashCode(),
                obj.Item2.ToLowerInvariant().GetHashCode());
    }
}

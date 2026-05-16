using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

public interface IPermissionSeeder
{
    /// <summary>Ensures every Resource/Action/Permission catalog row exists. Global, idempotent.</summary>
    Task SeedCatalogAsync(CancellationToken ct = default);

    /// <summary>Ensures the 4 built-in roles exist for the tenant, wired with their permissions.</summary>
    Task SeedBuiltInRolesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Validates that every <see cref="Tracker.Filters.RequirePermissionAttribute"/> reference
    /// is backed by a real catalog row. Throws on mismatch so misconfigurations surface at boot.</summary>
    Task ValidateAttributeReferencesAsync(CancellationToken ct = default);
}

public class PermissionSeeder : IPermissionSeeder
{
    private readonly AppDbContext _db;

    public PermissionSeeder(AppDbContext db) => _db = db;

    private static readonly (string Code, string Name, string? Description, int SortOrder)[] _resources =
    {
        (Resources.Sheets,     "Glass sheets",       "Create, edit and move sheets through shopfloors.", 10),
        (Resources.Batches,    "Batches",            "Group sheets into batches for production runs.",  20),
        (Resources.Customers,  "Customers",          "Customer master data.",                            30),
        (Resources.Employees,  "Employees",          "Employee master data.",                            40),
        (Resources.Plants,     "Plants",             "Plant master and plant switching.",                50),
        (Resources.Shopfloors, "Shopfloors",         "Shopfloor master and sequencing.",                 60),
        (Resources.Processes,  "Processes",          "Process master data.",                             70),
        (Resources.Users,      "Users",              "Invite, update and deactivate users.",             80),
        (Resources.Roles,      "Roles & permissions","Create roles and grant permissions.",              90),
        (Resources.Reports,    "Reports",            "Run and export production reports.",              100),
        (Resources.Workspace,  "Workspace",          "Workspace name and high-level settings.",         110)
    };

    private static readonly (string Code, string Name, int SortOrder)[] _actions =
    {
        (Actions.View,   "View",   10),
        (Actions.Add,    "Add",    20),
        (Actions.Edit,   "Edit",   30),
        (Actions.Delete, "Delete", 40)
    };

    public async Task SeedCatalogAsync(CancellationToken ct = default)
    {
        var existingResources = await _db.PermResources.ToDictionaryAsync(r => r.Code, ct);
        foreach (var (code, name, desc, sort) in _resources)
        {
            if (!existingResources.ContainsKey(code))
            {
                _db.PermResources.Add(new PermResource
                {
                    Code = code, Name = name, Description = desc, SortOrder = sort, IsSystem = true
                });
            }
        }

        var existingActions = await _db.PermActions.ToDictionaryAsync(a => a.Code, ct);
        foreach (var (code, name, sort) in _actions)
        {
            if (!existingActions.ContainsKey(code))
            {
                _db.PermActions.Add(new PermAction
                {
                    Code = code, Name = name, SortOrder = sort, IsSystem = true
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Now make sure every (resource, action) pair has a Permission row.
        var resources = await _db.PermResources.ToListAsync(ct);
        var actions = await _db.PermActions.ToListAsync(ct);
        var existingPairs = await _db.Permissions
            .Select(p => new { p.ResourceId, p.ActionId })
            .ToListAsync(ct);
        var pairSet = existingPairs.Select(x => (x.ResourceId, x.ActionId)).ToHashSet();

        foreach (var res in resources)
        {
            foreach (var act in actions)
            {
                if (!pairSet.Contains((res.Id, act.Id)))
                {
                    _db.Permissions.Add(new Permission { ResourceId = res.Id, ActionId = act.Id });
                }
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SeedBuiltInRolesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var allPerms = await _db.Permissions
            .Include(p => p.Resource)
            .Include(p => p.Action)
            .ToListAsync(ct);

        // Admin role — gets the IsSystemAdmin flag so it bypasses RolePermissions entirely.
        await EnsureRoleAsync(tenantId, SystemRoles.Admin, 1,
            "Full access to everything in the workspace.",
            isSystemAdmin: true, isSystem: true,
            permissions: Array.Empty<Permission>(), ct);

        // Manager — View/Add/Edit on every resource, plus Reports.View.
        await EnsureRoleAsync(tenantId, SystemRoles.Manager, 2,
            "View, add and edit. Cannot delete.",
            isSystemAdmin: false, isSystem: true,
            permissions: allPerms.Where(p =>
                p.Action.Code != Actions.Delete &&
                p.Resource.Code != Resources.Roles &&
                p.Resource.Code != Resources.Users).ToList(),
            ct);

        // Operator — Sheets/Batches: full CRUD minus Delete; everything else: View only.
        await EnsureRoleAsync(tenantId, SystemRoles.Operator, 3,
            "Day-to-day floor operator: works with sheets and batches.",
            isSystemAdmin: false, isSystem: true,
            permissions: allPerms.Where(p =>
                (p.Resource.Code is Resources.Sheets or Resources.Batches &&
                 p.Action.Code != Actions.Delete) ||
                p.Action.Code == Actions.View).ToList(),
            ct);

        // Viewer — View on every resource.
        await EnsureRoleAsync(tenantId, SystemRoles.Viewer, 4,
            "Read-only access including reports.",
            isSystemAdmin: false, isSystem: true,
            permissions: allPerms.Where(p => p.Action.Code == Actions.View).ToList(),
            ct);
    }

    private async Task EnsureRoleAsync(
        Guid tenantId, string name, int number, string description,
        bool isSystemAdmin, bool isSystem, IReadOnlyList<Permission> permissions,
        CancellationToken ct)
    {
        var role = await _db.RoleDefinitions
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name, ct);

        if (role is null)
        {
            // Find a free Number — if the tenant has custom roles already, our preferred
            // number may collide. Bump to max+1 in that case.
            var preferred = number;
            var inUse = await _db.RoleDefinitions
                .AnyAsync(r => r.TenantId == tenantId && r.Number == preferred, ct);
            if (inUse)
            {
                var maxN = await _db.RoleDefinitions
                    .Where(r => r.TenantId == tenantId)
                    .MaxAsync(r => (int?)r.Number, ct) ?? 0;
                preferred = maxN + 1;
            }

            role = new RoleDefinition
            {
                TenantId = tenantId,
                Number = preferred,
                Name = name,
                Description = description,
                IsSystemAdmin = isSystemAdmin,
                IsSystem = isSystem,
                IsActive = true
            };
            _db.RoleDefinitions.Add(role);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Re-assert flags in case a previous migration left them off.
            if (role.IsSystemAdmin != isSystemAdmin) role.IsSystemAdmin = isSystemAdmin;
            if (role.IsSystem != isSystem) role.IsSystem = isSystem;
            await _db.SaveChangesAsync(ct);
        }

        // Skip permission rows entirely for IsSystemAdmin — the resolver short-circuits.
        if (isSystemAdmin) return;

        var existing = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId).ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        foreach (var perm in permissions)
        {
            if (!existingSet.Contains(perm.Id))
            {
                _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ValidateAttributeReferencesAsync(CancellationToken ct = default)
    {
        var resourceCodes = (await _db.PermResources.Select(r => r.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actionCodes = (await _db.PermActions.Select(a => a.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assemblyTypes = typeof(PermissionSeeder).Assembly.GetTypes();
        var missing = new List<string>();
        foreach (var type in assemblyTypes)
        {
            foreach (var method in type.GetMethods())
            {
                var attrs = method.GetCustomAttributes(typeof(Filters.RequirePermissionAttribute), false);
                foreach (Filters.RequirePermissionAttribute attr in attrs)
                {
                    if (!resourceCodes.Contains(attr.ResourceCode))
                        missing.Add($"{type.FullName}.{method.Name}: resource '{attr.ResourceCode}' not in DB");
                    if (!actionCodes.Contains(attr.ActionCode))
                        missing.Add($"{type.FullName}.{method.Name}: action '{attr.ActionCode}' not in DB");
                }
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Permission attribute validation failed:\n  - " + string.Join("\n  - ", missing));
        }
    }
}

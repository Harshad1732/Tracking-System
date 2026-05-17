using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// A named bundle of permissions, scoped to one tenant. Permissions are stored as
/// rows in <see cref="RolePermissions"/> rather than columns on this entity — that lets
/// us add new resources/actions without schema changes.
/// </summary>
public class RoleDefinition : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    /// <summary>
    /// When true, holders of this role get every permission without consulting the
    /// RolePermissions table. There is at most one such role per tenant — it's the
    /// successor of the old "Admin" magic string.
    /// </summary>
    public bool IsSystemAdmin { get; set; }

    /// <summary>
    /// When true, this role was seeded by the system. The UI prevents rename/delete so
    /// upgrades that depend on a known role-name stay stable.
    /// </summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRoleAssignment> Assignments { get; set; } = new List<UserRoleAssignment>();
}

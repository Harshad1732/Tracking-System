namespace Tracker.Entities;

/// <summary>
/// One row per (Resource, Action) pair. Roles get linked to these via RolePermissions —
/// keeping the join two-step (Role -&gt; Permission -&gt; Resource/Action) means we can carry
/// metadata on a permission later (description, category, deprecated flag) without
/// touching the role schema.
/// </summary>
public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResourceId { get; set; }
    public PermResource Resource { get; set; } = null!;

    public Guid ActionId { get; set; }
    public PermAction Action { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

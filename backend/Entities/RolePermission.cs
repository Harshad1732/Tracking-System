namespace Tracker.Entities;

/// <summary>
/// Grants one Permission to one Role within a tenant. Composite PK (RoleId, PermissionId).
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public RoleDefinition Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

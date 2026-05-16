using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// Scope of a role assignment. Stored as a string in the DB to avoid enum-migration churn
/// when new scope types are added later (e.g. Shopfloor).
/// </summary>
public static class AssignmentScope
{
    public const string Tenant = "Tenant";
    public const string Plant = "Plant";
}

/// <summary>
/// Assigns a user one role at a specific scope. A user may hold multiple assignments;
/// effective permissions = union across all matching assignments.
///
///  • ScopeType = "Tenant"  → applies in every plant in the tenant. ScopeId is null.
///  • ScopeType = "Plant"   → applies only when the request's current plant matches ScopeId.
/// </summary>
public class UserRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public RoleDefinition Role { get; set; } = null!;

    [Required, MaxLength(40)]
    public string ScopeType { get; set; } = AssignmentScope.Tenant;

    /// <summary>Null when ScopeType == Tenant. Otherwise the FK target id (e.g. Plant.Id).</summary>
    public Guid? ScopeId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
}

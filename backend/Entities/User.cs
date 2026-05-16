using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? FullName { get; set; }

    public string? PasswordHash { get; set; }

    [MaxLength(40)]
    public string? Provider { get; set; }

    [MaxLength(256)]
    public string? ProviderUserId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Default plant for the user. The session can switch to other plants for which the
    /// user holds a Plant-scoped or Tenant-scoped role assignment. Null = no preferred
    /// default — the session picks the lowest-numbered active plant in the tenant.
    /// </summary>
    public Guid? PlantId { get; set; }
    public Plant? Plant { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<UserRoleAssignment> RoleAssignments { get; set; } = new List<UserRoleAssignment>();
}

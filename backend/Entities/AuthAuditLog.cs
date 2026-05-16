using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// Append-only log of authorization changes — role created/edited/deleted, permission
/// granted/revoked, user assignment added/removed. Records who, when, what target.
/// Survives deletion of referenced rows (no FKs) so audits remain reviewable.
/// </summary>
public class AuthAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? TenantId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ActorUserId { get; set; }
    [MaxLength(256)] public string? ActorEmail { get; set; }

    [Required, MaxLength(80)]  public string Action { get; set; } = string.Empty;
    [MaxLength(40)]  public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    [MaxLength(2000)] public string? Details { get; set; }
}

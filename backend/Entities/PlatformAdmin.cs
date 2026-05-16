namespace Tracker.Entities;

/// <summary>
/// Cross-tenant super-user grant. Moved off <see cref="User"/> so a platform concern
/// doesn't leak into tenant data and grants can be revoked without touching the user row.
/// </summary>
public class PlatformAdmin
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? GrantedByUserId { get; set; }
}

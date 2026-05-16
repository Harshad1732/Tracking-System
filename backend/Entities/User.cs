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

    [Required, MaxLength(40)]
    public string Role { get; set; } = "User";

    [MaxLength(40)]
    public string? Provider { get; set; }

    [MaxLength(256)]
    public string? ProviderUserId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}

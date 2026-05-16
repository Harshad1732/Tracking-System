using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? ContactPerson { get; set; }

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

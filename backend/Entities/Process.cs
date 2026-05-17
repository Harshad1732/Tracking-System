using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Process : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public int SequenceNo { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
}

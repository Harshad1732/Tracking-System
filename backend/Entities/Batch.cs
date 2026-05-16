using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Batch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Plant this batch belongs to. Inherited from the shopfloor it was formed on.</summary>
    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(40)]
    public string BatchNo { get; set; } = string.Empty;

    public Guid CurrentShopfloorId { get; set; }
    public Shopfloor CurrentShopfloor { get; set; } = null!;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "InProcess";

    [MaxLength(250)]
    public string? Remarks { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastMovedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public ICollection<GlassSheet> Sheets { get; set; } = new List<GlassSheet>();
}

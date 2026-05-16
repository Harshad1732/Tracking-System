using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class GlassSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Plant this sheet belongs to. Inherited from the shopfloor it was created on.</summary>
    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(60)]
    public string SheetNo { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? OrderNo { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [MaxLength(60)]
    public string? GlassType { get; set; }

    public decimal? Thickness { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public int Quantity { get; set; } = 1;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Pending, InProcess, Completed, Delivered, Hold

    public Guid CurrentShopfloorId { get; set; }
    public Shopfloor CurrentShopfloor { get; set; } = null!;

    public Guid? BatchId { get; set; }
    public Batch? Batch { get; set; }

    [MaxLength(250)]
    public string? Remarks { get; set; }

    /// <summary>
    /// If this sheet was created to replace another (typically a Rejected or Hold sheet),
    /// this points at the original. Null for regular sheets. The chain is intentionally
    /// shallow — a replacement of a replacement is allowed and forms a chain via this field.
    /// </summary>
    public Guid? ReplacementForSheetId { get; set; }
    public GlassSheet? ReplacementForSheet { get; set; }

    [MaxLength(500)]
    public string? ReplacementReason { get; set; }

    public DateTime EntryAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastMovedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<SheetMovement> Movements { get; set; } = new List<SheetMovement>();
}

using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class SheetMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid GlassSheetId { get; set; }
    public GlassSheet GlassSheet { get; set; } = null!;

    public Guid? FromShopfloorId { get; set; }
    public Shopfloor? FromShopfloor { get; set; }

    public Guid ToShopfloorId { get; set; }
    public Shopfloor ToShopfloor { get; set; } = null!;

    public Guid? MovedByUserId { get; set; }
    public User? MovedByUser { get; set; }

    [MaxLength(250)]
    public string? Remarks { get; set; }

    [MaxLength(30)]
    public string? Status { get; set; }

    public DateTime MovedAtUtc { get; set; } = DateTime.UtcNow;
}

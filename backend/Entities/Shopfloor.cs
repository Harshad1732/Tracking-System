using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Shopfloor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Which plant this floor belongs to. A tenant with multiple plants keeps each plant's
    /// floors fully isolated — sheets on one plant's SF1 never appear in another plant's lists.
    /// </summary>
    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public int SequenceNo { get; set; }

    /// <summary>True for the entry/storage location. Sheets land here first.</summary>
    public bool IsStorage { get; set; }

    /// <summary>"None" | "AutoConfirm" | "Manual" — controls batch behaviour on this floor.</summary>
    [Required, MaxLength(20)]
    public string BatchMode { get; set; } = "None";

    /// <summary>
    /// SheetStatus.Code that sheets get when they arrive on this floor (from a move).
    /// Defaults during seed: storage floors → "Pending", production floors → "InProcess".
    /// Replaces the old hardcoded <c>target.IsStorage ? "Pending" : "InProcess"</c>.
    /// </summary>
    [MaxLength(40)]
    public string? ArrivalStatusCode { get; set; }

    /// <summary>
    /// Optional explicit tile color as a 7-char hex string (e.g. "#2563eb"). When set,
    /// overrides the auto-palette in the workspace pipeline and dashboard. Null = let the
    /// frontend pick a colour from its sequence-based palette.
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? ProcessId { get; set; }
    public Process? Process { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

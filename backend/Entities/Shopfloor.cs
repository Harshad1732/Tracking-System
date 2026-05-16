using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Shopfloor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

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

    public bool IsActive { get; set; } = true;

    public Guid? ProcessId { get; set; }
    public Process? Process { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid? PlantId { get; set; }
    public Plant? Plant { get; set; }

    public Guid? ProcessId { get; set; }
    public Process? Process { get; set; }

    /// <summary>Sequential per-tenant display number. Auto-assigned on create.</summary>
    public int Number { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [MaxLength(60)]
    public string? Department { get; set; }

    [MaxLength(60)]
    public string? Designation { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

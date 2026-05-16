using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// Catalog of valid statuses a glass sheet (or batch) can be in. Seeded with the
/// default set ("Pending", "InProcess", "Completed", "Hold", "Rejected", "Delivered")
/// and editable per tenant later (e.g. add a custom "QA-Hold"). Replaces the
/// hardcoded HashSets that previously lived in <c>SheetsController</c> and
/// <c>BatchesController</c>.
///
/// Codes are global (not per-tenant) for now — multi-tenant overrides can be added
/// later via a nullable TenantId column without breaking existing rows.
/// </summary>
public class SheetStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>True for the status used when a sheet is first created (default: Pending).</summary>
    public bool IsInitial { get; set; }

    /// <summary>Terminal state — no further status transitions expected (default: Delivered).</summary>
    public bool IsTerminal { get; set; }

    /// <summary>When true, sheets in this status can have a "replacement" issued
    /// (default: Hold, Rejected). Replaces the old hardcoded REPLACEABLE set.</summary>
    public bool IsReplaceable { get; set; }

    /// <summary>Whether this status is valid on a GlassSheet.</summary>
    public bool AppliesToSheets { get; set; } = true;

    /// <summary>Whether this status is valid on a Batch.</summary>
    public bool AppliesToBatches { get; set; } = true;

    /// <summary>Seeded by the system — UI hides delete to prevent reference breakage.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Inactive statuses are kept for historical data but excluded from pickers.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

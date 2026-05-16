using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// A protected area of the app (e.g. "Sheets", "Batches"). Resources live in the DB,
/// not in a C# enum, so a new resource can be added without redeploying.
/// </summary>
public class PermResource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(60)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Seeded resource — UI hides delete/rename to prevent reference breakage.</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

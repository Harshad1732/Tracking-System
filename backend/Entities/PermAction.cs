using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

/// <summary>
/// A verb that can be performed on a resource (e.g. "View", "Add", "Edit", "Delete").
/// DB-driven for the same reason as <see cref="PermResource"/>.
/// </summary>
public class PermAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

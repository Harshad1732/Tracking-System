using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable identifier (free, starter, pro, enterprise). Used in code + URLs.</summary>
    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    /// <summary>Monthly price in the smallest currency unit (e.g. paise/cents). 0 = free.</summary>
    public int MonthlyPriceCents { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "USD";

    public int MaxSheets { get; set; }
    public int MaxUsers { get; set; }
    public int MaxShopfloors { get; set; }

    /// <summary>Days of movement/sheet history kept before archival. -1 = unlimited.</summary>
    public int RetentionDays { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Optional Stripe Price ID for upgrade checkout sessions.</summary>
    [MaxLength(120)]
    public string? StripePriceId { get; set; }
}

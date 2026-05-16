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

    /// <summary>How long the free trial lasts when a tenant signs up on this plan.
    /// 0 = no trial (paid plans). Replaces the old hardcoded "14 days everywhere".</summary>
    public int TrialDays { get; set; }

    /// <summary>Length of each billing period in months. Default 1 (monthly).
    /// Replaces the old hardcoded <c>DateTime.UtcNow.AddMonths(1)</c>.</summary>
    public int BillingIntervalMonths { get; set; } = 1;

    /// <summary>
    /// Exactly one plan should be flagged true — it's the plan a brand-new tenant lands
    /// on when registering via the public sign-up flow. Replaces the hardcoded
    /// <c>p.Code == "free"</c> lookup in AuthService. Toggleable from the Plans UI.
    /// </summary>
    public bool IsDefaultOnSignup { get; set; }
}

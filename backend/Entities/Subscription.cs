using System.ComponentModel.DataAnnotations;

namespace Tracker.Entities;

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    /// <summary>"Trial" | "Active" | "PastDue" | "Canceled" | "Expired".</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Trial";

    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }
    public DateTime? CanceledAtUtc { get; set; }

    [MaxLength(120)]
    public string? StripeCustomerId { get; set; }

    [MaxLength(120)]
    public string? StripeSubscriptionId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

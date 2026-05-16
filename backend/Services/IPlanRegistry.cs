using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

/// <summary>
/// Read-side access to the Plans catalog. Removes the last hardcoded plan-code
/// lookups (<c>p.Code == "free"</c>) by exposing intent-named queries:
///   <c>GetDefaultSignupPlanAsync()</c> instead of "give me the free plan".
/// </summary>
public interface IPlanRegistry
{
    /// <summary>The plan that brand-new tenants land on at registration. Resolved by
    /// the <see cref="Plan.IsDefaultOnSignup"/> flag — falls back to lowest SortOrder
    /// among active plans if no plan is flagged.</summary>
    Task<Plan?> GetDefaultSignupPlanAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Plan>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
}

public class PlanRegistry : IPlanRegistry
{
    private readonly AppDbContext _db;
    public PlanRegistry(AppDbContext db) => _db = db;

    public async Task<Plan?> GetDefaultSignupPlanAsync(CancellationToken ct = default)
    {
        // Prefer the explicit flag. If none is flagged (misconfig), fall back deterministically
        // to the lowest-SortOrder active plan so sign-up still works rather than 500-ing.
        var flagged = await _db.Plans.AsNoTracking()
            .Where(p => p.IsDefaultOnSignup && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .FirstOrDefaultAsync(ct);
        if (flagged is not null) return flagged;

        return await _db.Plans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Plan>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var q = _db.Plans.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

public interface IPlanLimitService
{
    Task<Subscription?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct);
    Task<LimitCheckResult> CheckSheetsAsync(Guid tenantId, int adding, CancellationToken ct);
    Task<LimitCheckResult> CheckUsersAsync(Guid tenantId, int adding, CancellationToken ct);
    Task<LimitCheckResult> CheckShopfloorsAsync(Guid tenantId, int adding, CancellationToken ct);
}

public record LimitCheckResult(bool Allowed, int Limit, int Current, string ResourceName)
{
    public string ErrorMessage =>
        $"You've reached your plan's limit of {Limit} {ResourceName}. Upgrade your plan to add more.";
}

public class PlanLimitService : IPlanLimitService
{
    private readonly AppDbContext _db;
    public PlanLimitService(AppDbContext db) => _db = db;

    public Task<Subscription?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct) =>
        _db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public async Task<LimitCheckResult> CheckSheetsAsync(Guid tenantId, int adding, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(tenantId, ct);
        if (sub is null) return new LimitCheckResult(true, int.MaxValue, 0, "sheets");
        var current = await _db.GlassSheets.CountAsync(g => g.TenantId == tenantId, ct);
        var limit = sub.Plan.MaxSheets;
        return new LimitCheckResult(current + adding <= limit, limit, current, "sheets");
    }

    public async Task<LimitCheckResult> CheckUsersAsync(Guid tenantId, int adding, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(tenantId, ct);
        if (sub is null) return new LimitCheckResult(true, int.MaxValue, 0, "users");
        var current = await _db.Users.CountAsync(u => u.TenantId == tenantId, ct);
        var limit = sub.Plan.MaxUsers;
        return new LimitCheckResult(current + adding <= limit, limit, current, "users");
    }

    public async Task<LimitCheckResult> CheckShopfloorsAsync(Guid tenantId, int adding, CancellationToken ct)
    {
        var sub = await GetSubscriptionAsync(tenantId, ct);
        if (sub is null) return new LimitCheckResult(true, int.MaxValue, 0, "shopfloors");
        var current = await _db.Shopfloors.CountAsync(s => s.TenantId == tenantId, ct);
        var limit = sub.Plan.MaxShopfloors;
        return new LimitCheckResult(current + adding <= limit, limit, current, "shopfloors");
    }
}

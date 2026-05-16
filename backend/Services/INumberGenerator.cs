using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

/// <summary>
/// Assigns the next sequential per-tenant <c>Number</c> when creating any
/// numbered entity. We compute MAX(Number)+1 inside the same SaveChanges,
/// which is fine at the insert throughput of a typical shopfloor. Holes
/// (1, 2, 4) appear naturally when inserts fail / are rolled back.
/// </summary>
public interface INumberGenerator
{
    Task<int> NextPlantAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextProcessAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextEmployeeAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextCustomerAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextShopfloorAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextRoleAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextSheetAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextBatchAsync(Guid tenantId, CancellationToken ct);
    Task<int> NextUserAsync(Guid tenantId, CancellationToken ct);
}

public class NumberGenerator : INumberGenerator
{
    private readonly AppDbContext _db;
    public NumberGenerator(AppDbContext db) => _db = db;

    public Task<int> NextPlantAsync(Guid t, CancellationToken ct)     => Next(_db.Plants,          t, ct);
    public Task<int> NextProcessAsync(Guid t, CancellationToken ct)   => Next(_db.Processes,       t, ct);
    public Task<int> NextEmployeeAsync(Guid t, CancellationToken ct)  => Next(_db.Employees,       t, ct);
    public Task<int> NextCustomerAsync(Guid t, CancellationToken ct)  => Next(_db.Customers,       t, ct);
    public Task<int> NextShopfloorAsync(Guid t, CancellationToken ct) => Next(_db.Shopfloors,      t, ct);
    public Task<int> NextRoleAsync(Guid t, CancellationToken ct)      => Next(_db.RoleDefinitions, t, ct);
    public Task<int> NextSheetAsync(Guid t, CancellationToken ct)     => Next(_db.GlassSheets,     t, ct);
    public Task<int> NextBatchAsync(Guid t, CancellationToken ct)     => Next(_db.Batches,         t, ct);
    public Task<int> NextUserAsync(Guid t, CancellationToken ct)      => Next(_db.Users,           t, ct);

    private static async Task<int> Next<T>(IQueryable<T> source, Guid tenantId, CancellationToken ct)
        where T : class
    {
        // EF Core can't statically know T has TenantId/Number — use reflection-friendly form.
        var max = await source
            .Where(BuildTenantPredicate<T>(tenantId))
            .Select(e => EF.Property<int>(e, "Number"))
            .DefaultIfEmpty(0)
            .MaxAsync(ct);
        return max + 1;
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildTenantPredicate<T>(Guid tenantId)
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        var prop = System.Linq.Expressions.Expression.Property(p, "TenantId");
        var val = System.Linq.Expressions.Expression.Constant(tenantId);
        var eq = System.Linq.Expressions.Expression.Equal(prop, val);
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(eq, p);
    }
}

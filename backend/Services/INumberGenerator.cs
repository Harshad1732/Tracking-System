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

    /// <summary>
    /// Builds a display code of the form "{prefix}-{number:D3}" — e.g. <c>FormatCode("PLT", 1) == "PLT-001"</c>.
    /// Replaces the four inline copies that lived in PlantsController, EmployeesController, CustomersController,
    /// ProcessesController. Single source of truth — change the format here, every master picks it up.
    /// </summary>
    static string FormatCode(string prefix, int number) => $"{prefix}-{number:D3}";

    /// <summary>
    /// Generates the next per-day batch number ("B-YYMMDD-NNN") for the given tenant.
    /// Replaces the two identical inline implementations in SheetsController and BatchesController.
    /// </summary>
    Task<string> NextBatchNoAsync(Guid tenantId, CancellationToken ct);
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

    public async Task<string> NextBatchNoAsync(Guid tenantId, CancellationToken ct)
    {
        // BatchNo format: B-YYMMDD-NNN (per-day sequence per tenant). Single source of truth —
        // both SheetsController.Move (when auto-creating a batch on move) and BatchesController.Create
        // call this rather than each implementing the same regex-y SQL.
        var today = DateTime.UtcNow.ToString("yyMMdd");
        var prefix = $"B-{today}-";
        var existing = await _db.Batches
            .Where(b => b.TenantId == tenantId && b.BatchNo.StartsWith(prefix))
            .Select(b => b.BatchNo)
            .ToListAsync(ct);
        var next = existing.Count == 0
            ? 1
            : existing.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).Max() + 1;
        return $"{prefix}{next:D3}";
    }

    private static async Task<int> Next<T>(IQueryable<T> source, Guid tenantId, CancellationToken ct)
        where T : class
    {
        // The previous form — `.Select(int).DefaultIfEmpty(0).MaxAsync()` — fails to
        // translate in EF Core 8 ("expression could not be translated"). Projecting to
        // nullable and coalescing in C# is the canonical workaround. EF emits a single
        // `SELECT MAX([Number])` and returns NULL when the table is empty for this tenant.
        var max = await source
            .Where(BuildTenantPredicate<T>(tenantId))
            .MaxAsync(BuildNullableNumberSelector<T>(), ct);
        return (max ?? 0) + 1;
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildTenantPredicate<T>(Guid tenantId)
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        var prop = System.Linq.Expressions.Expression.Property(p, "TenantId");
        var val = System.Linq.Expressions.Expression.Constant(tenantId);
        var eq = System.Linq.Expressions.Expression.Equal(prop, val);
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(eq, p);
    }

    /// <summary>Builds <c>e =&gt; (int?)e.Number</c> for arbitrary T with a Number column.</summary>
    private static System.Linq.Expressions.Expression<Func<T, int?>> BuildNullableNumberSelector<T>()
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(T), "e");
        var prop = System.Linq.Expressions.Expression.Property(p, "Number");
        var asNullable = System.Linq.Expressions.Expression.Convert(prop, typeof(int?));
        return System.Linq.Expressions.Expression.Lambda<Func<T, int?>>(asNullable, p);
    }
}

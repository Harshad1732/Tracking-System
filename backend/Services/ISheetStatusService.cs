using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Entities;

namespace Tracker.Services;

public interface ISheetStatusService
{
    /// <summary>All active statuses, cached per scope. Pass a filter to narrow to sheets/batches.</summary>
    Task<IReadOnlyList<SheetStatus>> ListAsync(bool? forSheets = null, bool? forBatches = null, CancellationToken ct = default);

    /// <summary>Validates that a given code is an active status that applies to the target. Case-insensitive.</summary>
    Task<bool> IsValidAsync(string code, bool forSheets, CancellationToken ct = default);

    Task<SheetStatus?> FindByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Returns the code of the status flagged <c>IsInitial</c> (e.g. "Pending"). Throws if no row marked.</summary>
    Task<string> InitialStatusCodeAsync(CancellationToken ct = default);
}

public class SheetStatusService : ISheetStatusService
{
    private readonly AppDbContext _db;
    public SheetStatusService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SheetStatus>> ListAsync(bool? forSheets = null, bool? forBatches = null, CancellationToken ct = default)
    {
        var q = _db.SheetStatuses.AsNoTracking().Where(s => s.IsActive);
        if (forSheets == true) q = q.Where(s => s.AppliesToSheets);
        if (forBatches == true) q = q.Where(s => s.AppliesToBatches);
        return await q.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(ct);
    }

    public async Task<bool> IsValidAsync(string code, bool forSheets, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return await _db.SheetStatuses
            .AsNoTracking()
            .AnyAsync(s => s.IsActive
                        && s.Code == code
                        && (forSheets ? s.AppliesToSheets : s.AppliesToBatches), ct);
    }

    public Task<SheetStatus?> FindByCodeAsync(string code, CancellationToken ct = default) =>
        _db.SheetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Code == code, ct);

    public async Task<string> InitialStatusCodeAsync(CancellationToken ct = default)
    {
        var s = await _db.SheetStatuses.AsNoTracking()
            .Where(x => x.IsActive && x.IsInitial && x.AppliesToSheets)
            .OrderBy(x => x.SortOrder)
            .FirstOrDefaultAsync(ct);
        if (s is null)
            throw new InvalidOperationException("No SheetStatus row is flagged IsInitial. Seed the catalog.");
        return s.Code;
    }
}

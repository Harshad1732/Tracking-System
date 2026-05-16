using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
using Tracker.Filters;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Authorize]
[Route("api/shopfloors")]
public class ShopfloorsController : TenantControllerBase
{
    private static readonly HashSet<string> AllowedBatchModes =
        new(StringComparer.OrdinalIgnoreCase) { "None", "AutoConfirm", "Manual" };

    private readonly AppDbContext _db;
    private readonly IPlanLimitService _limits;
    private readonly INumberGenerator _ng;
    public ShopfloorsController(AppDbContext db, IPlanLimitService limits, INumberGenerator ng)
    {
        _db = db;
        _limits = limits;
        _ng = ng;
    }

    [HttpGet]
    [RequirePermission(Resources.Shopfloors, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<ShopfloorDto>>> List(CancellationToken ct)
    {
        var items = await _db.Shopfloors.AsNoTracking()
            .Where(s => s.TenantId == TenantId && s.PlantId == PlantId)
            .OrderBy(s => s.SequenceNo).ThenBy(s => s.Name)
            .Select(s => new ShopfloorDto(
                s.Id, s.Number, s.Code, s.Name, s.SequenceNo, s.IsStorage,
                s.BatchMode,
                s.ProcessId, s.Process != null ? s.Process.Name : null,
                _db.GlassSheets.Count(g => g.CurrentShopfloorId == s.Id),
                s.Color,
                s.IsActive, s.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(Resources.Shopfloors, Actions.Add)]
    public async Task<ActionResult<ShopfloorDto>> Create(ShopfloorUpsertRequest req, CancellationToken ct)
    {
        if (!AllowedBatchModes.Contains(req.BatchMode))
            return BadRequest(new { error = $"Unknown batch mode '{req.BatchMode}'." });
        var limit = await _limits.CheckShopfloorsAsync(TenantId, 1, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });
        if (req.ProcessId is { } prid && !await _db.Processes.AnyAsync(p => p.Id == prid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Process not found." });

        var code = await GenerateShopfloorCodeAsync(req.IsStorage, ct);
        var item = new Shopfloor
        {
            TenantId = TenantId,
            PlantId = PlantId,
            Number = await _ng.NextShopfloorAsync(TenantId, ct),
            Code = code,
            Name = req.Name,
            SequenceNo = req.SequenceNo,
            IsStorage = req.IsStorage,
            BatchMode = req.BatchMode,
            ProcessId = req.ProcessId,
            Color = NormalizeHex(req.Color),
            IsActive = req.IsActive
        };
        _db.Shopfloors.Add(item);
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Resources.Shopfloors, Actions.Edit)]
    public async Task<ActionResult<ShopfloorDto>> Update(Guid id, ShopfloorUpsertRequest req, CancellationToken ct)
    {
        if (!AllowedBatchModes.Contains(req.BatchMode))
            return BadRequest(new { error = $"Unknown batch mode '{req.BatchMode}'." });
        var item = await _db.Shopfloors.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId && s.PlantId == PlantId, ct);
        if (item is null) return NotFound();
        if (req.ProcessId is { } prid && !await _db.Processes.AnyAsync(p => p.Id == prid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Process not found." });

        // Code is immutable post-creation. If the IsStorage flag flips, we deliberately
        // keep the original code — a "STORAGE" floor that turns into a normal floor
        // would otherwise have a confusing pre-existing code. Renames go through Name.
        item.Name = req.Name;
        item.SequenceNo = req.SequenceNo;
        item.IsStorage = req.IsStorage;
        item.BatchMode = req.BatchMode;
        item.ProcessId = req.ProcessId;
        item.Color = NormalizeHex(req.Color);
        item.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    // Accept "#RRGGBB" or null/empty (= no override). Anything else is rejected silently
    // by clearing the field, so a bad payload can't corrupt the column.
    private static string? NormalizeHex(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim();
        if (s.Length != 7 || s[0] != '#') return null;
        for (var i = 1; i < 7; i++)
        {
            var c = s[i];
            var ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return null;
        }
        return s.ToLowerInvariant();
    }

    // Generates the next shopfloor code for the CURRENT plant. Storage floors get
    // "STORAGE" (or "STORAGE-N" if the plant somehow has more than one); other floors
    // get sequential "SF1", "SF2"... numbered among non-storage floors in this plant only.
    private async Task<string> GenerateShopfloorCodeAsync(bool isStorage, CancellationToken ct)
    {
        if (isStorage)
        {
            var existingStorage = await _db.Shopfloors
                .CountAsync(s => s.PlantId == PlantId && s.IsStorage, ct);
            return existingStorage == 0 ? "STORAGE" : $"STORAGE-{existingStorage + 1}";
        }
        var existingFloors = await _db.Shopfloors
            .CountAsync(s => s.PlantId == PlantId && !s.IsStorage, ct);
        return $"SF{existingFloors + 1}";
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Shopfloors, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await _db.Shopfloors.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId && s.PlantId == PlantId, ct);
        if (item is null) return NotFound();
        if (await _db.GlassSheets.AnyAsync(g => g.CurrentShopfloorId == id, ct))
            return Conflict(new { error = "Shopfloor has sheets currently on it. Move them away first." });
        _db.Shopfloors.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<ShopfloorDto>> ToDto(Guid id, CancellationToken ct)
    {
        var dto = await _db.Shopfloors.AsNoTracking()
            .Where(s => s.Id == id && s.TenantId == TenantId && s.PlantId == PlantId)
            .Select(s => new ShopfloorDto(
                s.Id, s.Number, s.Code, s.Name, s.SequenceNo, s.IsStorage,
                s.BatchMode,
                s.ProcessId, s.Process != null ? s.Process.Name : null,
                _db.GlassSheets.Count(g => g.CurrentShopfloorId == s.Id),
                s.Color,
                s.IsActive, s.CreatedAtUtc))
            .FirstAsync(ct);
        return Ok(dto);
    }
}

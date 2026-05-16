using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
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
    public async Task<ActionResult<IReadOnlyList<ShopfloorDto>>> List(CancellationToken ct)
    {
        var items = await _db.Shopfloors.AsNoTracking()
            .Where(s => s.TenantId == TenantId)
            .OrderBy(s => s.SequenceNo).ThenBy(s => s.Name)
            .Select(s => new ShopfloorDto(
                s.Id, s.Number, s.Code, s.Name, s.SequenceNo, s.IsStorage,
                s.BatchMode,
                s.ProcessId, s.Process != null ? s.Process.Name : null,
                _db.GlassSheets.Count(g => g.CurrentShopfloorId == s.Id),
                s.IsActive, s.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ShopfloorDto>> Create(ShopfloorUpsertRequest req, CancellationToken ct)
    {
        if (!AllowedBatchModes.Contains(req.BatchMode))
            return BadRequest(new { error = $"Unknown batch mode '{req.BatchMode}'." });
        var limit = await _limits.CheckShopfloorsAsync(TenantId, 1, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });
        if (await _db.Shopfloors.AnyAsync(s => s.TenantId == TenantId && s.Code == req.Code, ct))
            return Conflict(new { error = "A shopfloor with this code already exists." });
        if (req.ProcessId is { } prid && !await _db.Processes.AnyAsync(p => p.Id == prid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Process not found." });

        var item = new Shopfloor
        {
            TenantId = TenantId,
            Number = await _ng.NextShopfloorAsync(TenantId, ct),
            Code = req.Code,
            Name = req.Name,
            SequenceNo = req.SequenceNo,
            IsStorage = req.IsStorage,
            BatchMode = req.BatchMode,
            ProcessId = req.ProcessId,
            IsActive = req.IsActive
        };
        _db.Shopfloors.Add(item);
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShopfloorDto>> Update(Guid id, ShopfloorUpsertRequest req, CancellationToken ct)
    {
        if (!AllowedBatchModes.Contains(req.BatchMode))
            return BadRequest(new { error = $"Unknown batch mode '{req.BatchMode}'." });
        var item = await _db.Shopfloors.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        if (await _db.Shopfloors.AnyAsync(s => s.TenantId == TenantId && s.Code == req.Code && s.Id != id, ct))
            return Conflict(new { error = "A shopfloor with this code already exists." });
        if (req.ProcessId is { } prid && !await _db.Processes.AnyAsync(p => p.Id == prid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Process not found." });

        item.Code = req.Code;
        item.Name = req.Name;
        item.SequenceNo = req.SequenceNo;
        item.IsStorage = req.IsStorage;
        item.BatchMode = req.BatchMode;
        item.ProcessId = req.ProcessId;
        item.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await _db.Shopfloors.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);
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
            .Where(s => s.Id == id)
            .Select(s => new ShopfloorDto(
                s.Id, s.Number, s.Code, s.Name, s.SequenceNo, s.IsStorage,
                s.BatchMode,
                s.ProcessId, s.Process != null ? s.Process.Name : null,
                _db.GlassSheets.Count(g => g.CurrentShopfloorId == s.Id),
                s.IsActive, s.CreatedAtUtc))
            .FirstAsync(ct);
        return Ok(dto);
    }
}

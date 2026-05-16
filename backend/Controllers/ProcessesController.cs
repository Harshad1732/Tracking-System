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
[Route("api/processes")]
public class ProcessesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public ProcessesController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    [HttpGet]
    [RequirePermission(Resources.Processes, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<ProcessDto>>> List(CancellationToken ct)
    {
        // Processes belong to a specific plant — only show the current plant's.
        var items = await _db.Processes.AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.PlantId == PlantId)
            .OrderBy(p => p.SequenceNo).ThenBy(p => p.Name)
            .Select(p => new ProcessDto(p.Id, p.Number, p.PlantId, p.Plant.Name, p.Code, p.Name, p.SequenceNo, p.IsActive, p.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(Resources.Processes, Actions.Add)]
    public async Task<ActionResult<ProcessDto>> Create(ProcessUpsertRequest req, CancellationToken ct)
    {
        if (!await _db.Plants.AnyAsync(p => p.Id == req.PlantId && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Plant not found." });

        var number = await _ng.NextProcessAsync(TenantId, ct);
        var item = new Process
        {
            TenantId = TenantId,
            Number = number,
            PlantId = req.PlantId,
            Code = INumberGenerator.FormatCode("PR", number),
            Name = req.Name,
            SequenceNo = req.SequenceNo,
            IsActive = req.IsActive
        };
        _db.Processes.Add(item);
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Resources.Processes, Actions.Edit)]
    public async Task<ActionResult<ProcessDto>> Update(Guid id, ProcessUpsertRequest req, CancellationToken ct)
    {
        var item = await _db.Processes.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        if (!await _db.Plants.AnyAsync(p => p.Id == req.PlantId && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Plant not found." });

        // Code is immutable post-creation.
        item.PlantId = req.PlantId;
        item.Name = req.Name;
        item.SequenceNo = req.SequenceNo;
        item.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Processes, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await _db.Processes.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        _db.Processes.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<ProcessDto>> ToDto(Guid id, CancellationToken ct)
    {
        var dto = await _db.Processes.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProcessDto(p.Id, p.Number, p.PlantId, p.Plant.Name, p.Code, p.Name, p.SequenceNo, p.IsActive, p.CreatedAtUtc))
            .FirstAsync(ct);
        return Ok(dto);
    }
}

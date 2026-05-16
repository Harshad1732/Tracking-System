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
[Route("api/processes")]
public class ProcessesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public ProcessesController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProcessDto>>> List(CancellationToken ct)
    {
        var items = await _db.Processes.AsNoTracking()
            .Where(p => p.TenantId == TenantId)
            .OrderBy(p => p.SequenceNo).ThenBy(p => p.Name)
            .Select(p => new ProcessDto(p.Id, p.Number, p.PlantId, p.Plant.Name, p.Code, p.Name, p.SequenceNo, p.IsActive, p.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ProcessDto>> Create(ProcessUpsertRequest req, CancellationToken ct)
    {
        if (!await _db.Plants.AnyAsync(p => p.Id == req.PlantId && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Plant not found." });
        if (await _db.Processes.AnyAsync(p => p.TenantId == TenantId && p.Code == req.Code, ct))
            return Conflict(new { error = "A process with this code already exists." });

        var item = new Process
        {
            TenantId = TenantId,
            Number = await _ng.NextProcessAsync(TenantId, ct),
            PlantId = req.PlantId,
            Code = req.Code,
            Name = req.Name,
            SequenceNo = req.SequenceNo,
            IsActive = req.IsActive
        };
        _db.Processes.Add(item);
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProcessDto>> Update(Guid id, ProcessUpsertRequest req, CancellationToken ct)
    {
        var item = await _db.Processes.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        if (!await _db.Plants.AnyAsync(p => p.Id == req.PlantId && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Plant not found." });
        if (await _db.Processes.AnyAsync(p => p.TenantId == TenantId && p.Code == req.Code && p.Id != id, ct))
            return Conflict(new { error = "A process with this code already exists." });

        item.PlantId = req.PlantId;
        item.Code = req.Code;
        item.Name = req.Name;
        item.SequenceNo = req.SequenceNo;
        item.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await ToDto(item.Id, ct);
    }

    [HttpDelete("{id:guid}")]
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

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
[Route("api/plants")]
public class PlantsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public PlantsController(AppDbContext db, INumberGenerator ng)
    {
        _db = db;
        _ng = ng;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlantDto>>> List(CancellationToken ct)
    {
        var items = await _db.Plants.AsNoTracking()
            .Where(p => p.TenantId == TenantId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PlantDto(p.Id, p.Number, p.Code, p.Name, p.Address, p.Phone, p.IsActive, p.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<PlantDto>> Create(PlantUpsertRequest req, CancellationToken ct)
    {
        if (await _db.Plants.AnyAsync(p => p.TenantId == TenantId && p.Code == req.Code, ct))
            return Conflict(new { error = "A plant with this code already exists." });

        var plant = new Plant
        {
            TenantId = TenantId,
            Number = await _ng.NextPlantAsync(TenantId, ct),
            Code = req.Code,
            Name = req.Name,
            Address = req.Address,
            Phone = req.Phone,
            IsActive = req.IsActive
        };
        _db.Plants.Add(plant);
        await _db.SaveChangesAsync(ct);
        return Ok(new PlantDto(plant.Id, plant.Number, plant.Code, plant.Name, plant.Address, plant.Phone, plant.IsActive, plant.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlantDto>> Update(Guid id, PlantUpsertRequest req, CancellationToken ct)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (plant is null) return NotFound();

        if (await _db.Plants.AnyAsync(p => p.TenantId == TenantId && p.Code == req.Code && p.Id != id, ct))
            return Conflict(new { error = "A plant with this code already exists." });

        plant.Code = req.Code;
        plant.Name = req.Name;
        plant.Address = req.Address;
        plant.Phone = req.Phone;
        plant.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new PlantDto(plant.Id, plant.Number, plant.Code, plant.Name, plant.Address, plant.Phone, plant.IsActive, plant.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (plant is null) return NotFound();
        _db.Plants.Remove(plant);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

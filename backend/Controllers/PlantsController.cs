using System.Security.Claims;
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
[Route("api/plants")]
public class PlantsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    private readonly IAuthService _auth;
    public PlantsController(AppDbContext db, INumberGenerator ng, IAuthService auth)
    {
        _db = db;
        _ng = ng;
        _auth = auth;
    }

    [HttpGet]
    [RequirePermission(Resources.Plants, Actions.View)]
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
    [RequirePermission(Resources.Plants, Actions.Add)]
    public async Task<ActionResult<PlantDto>> Create(PlantUpsertRequest req, CancellationToken ct)
    {
        var number = await _ng.NextPlantAsync(TenantId, ct);
        var plant = new Plant
        {
            TenantId = TenantId,
            Number = number,
            // Auto-generated via single source of truth — see INumberGenerator.FormatCode.
            Code = INumberGenerator.FormatCode("PLT", number),
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
    [RequirePermission(Resources.Plants, Actions.Edit)]
    public async Task<ActionResult<PlantDto>> Update(Guid id, PlantUpsertRequest req, CancellationToken ct)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (plant is null) return NotFound();

        // Code is immutable post-creation — auto-generated at create time and never edited.
        plant.Name = req.Name;
        plant.Address = req.Address;
        plant.Phone = req.Phone;
        plant.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new PlantDto(plant.Id, plant.Number, plant.Code, plant.Name, plant.Address, plant.Phone, plant.IsActive, plant.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Plants, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (plant is null) return NotFound();

        // Block deletion if the plant still owns any operational data — orphaning floors
        // and sheets would silently make them invisible to everyone (FK is Restrict).
        var hasFloors = await _db.Shopfloors.AnyAsync(s => s.PlantId == id, ct);
        var hasSheets = await _db.GlassSheets.AnyAsync(g => g.PlantId == id, ct);
        if (hasFloors || hasSheets)
            return Conflict(new { error = "This plant still has shopfloors or sheets. Move them to another plant first." });

        // Last-plant safety: a tenant must always have at least one plant so every user
        // and every new sheet has somewhere to land.
        var otherPlantCount = await _db.Plants.CountAsync(p => p.TenantId == TenantId && p.Id != id, ct);
        if (otherPlantCount == 0)
            return Conflict(new { error = "Can't delete the last plant in this workspace." });

        _db.Plants.Remove(plant);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // The plants the CURRENT user can switch into. Plant-locked users see only their
    // one plant; users with no lock see every active plant in the tenant.
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<PlantDto>>> Mine(CancellationToken ct)
    {
        var idRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idRaw, out var userId)) return Unauthorized();

        var me = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.TenantId == TenantId)
            .Select(u => new { u.PlantId })
            .FirstOrDefaultAsync(ct);
        if (me is null) return Unauthorized();

        var q = _db.Plants.AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.IsActive);
        if (me.PlantId is Guid locked) q = q.Where(p => p.Id == locked);

        var items = await q
            .OrderBy(p => p.Number)
            .Select(p => new PlantDto(p.Id, p.Number, p.Code, p.Name, p.Address, p.Phone, p.IsActive, p.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    // Switch the current user's plant context — issues a fresh JWT with `pid` set to
    // the target plant. Plant-locked users can only switch to their assigned plant.
    [HttpPost("switch/{plantId:guid}")]
    public async Task<ActionResult<AuthResponse>> Switch(Guid plantId, CancellationToken ct)
    {
        var idRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idRaw, out var userId)) return Unauthorized();

        var me = await _db.Users.Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == TenantId, ct);
        if (me is null || !me.IsActive) return Unauthorized();

        // Plant-locked users can only ever be in their own plant — refuse with 403.
        if (me.PlantId is Guid locked && locked != plantId)
            return Forbid();

        var target = await _db.Plants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == plantId && p.TenantId == TenantId && p.IsActive, ct);
        if (target is null) return NotFound(new { error = "Plant not found or inactive." });

        var result = await _auth.IssueTokensForPlantAsync(me, me.Tenant, target.Id, ct);
        return Ok(result);
    }
}

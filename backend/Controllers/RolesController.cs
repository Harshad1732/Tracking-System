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
[Route("api/roles")]
public class RolesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public RolesController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct)
    {
        var items = await _db.RoleDefinitions.AsNoTracking()
            .Where(r => r.TenantId == TenantId)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Number, r.Name, r.Description,
                r.CanView, r.CanAdd, r.CanEdit, r.CanDelete, r.CanViewReports,
                r.IsActive, r.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(RoleUpsertRequest req, CancellationToken ct)
    {
        if (await _db.RoleDefinitions.AnyAsync(r => r.TenantId == TenantId && r.Name == req.Name, ct))
            return Conflict(new { error = "A role with this name already exists." });

        var role = new RoleDefinition
        {
            TenantId = TenantId,
            Number = await _ng.NextRoleAsync(TenantId, ct),
            Name = req.Name,
            Description = req.Description,
            CanView = req.CanView,
            CanAdd = req.CanAdd,
            CanEdit = req.CanEdit,
            CanDelete = req.CanDelete,
            CanViewReports = req.CanViewReports,
            IsActive = req.IsActive
        };
        _db.RoleDefinitions.Add(role);
        await _db.SaveChangesAsync(ct);
        return Ok(new RoleDto(role.Id, role.Number, role.Name, role.Description,
            role.CanView, role.CanAdd, role.CanEdit, role.CanDelete, role.CanViewReports,
            role.IsActive, role.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Update(Guid id, RoleUpsertRequest req, CancellationToken ct)
    {
        var role = await _db.RoleDefinitions.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId, ct);
        if (role is null) return NotFound();
        if (await _db.RoleDefinitions.AnyAsync(r => r.TenantId == TenantId && r.Name == req.Name && r.Id != id, ct))
            return Conflict(new { error = "A role with this name already exists." });

        role.Name = req.Name;
        role.Description = req.Description;
        role.CanView = req.CanView;
        role.CanAdd = req.CanAdd;
        role.CanEdit = req.CanEdit;
        role.CanDelete = req.CanDelete;
        role.CanViewReports = req.CanViewReports;
        role.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new RoleDto(role.Id, role.Number, role.Name, role.Description,
            role.CanView, role.CanAdd, role.CanEdit, role.CanDelete, role.CanViewReports,
            role.IsActive, role.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var role = await _db.RoleDefinitions.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId, ct);
        if (role is null) return NotFound();
        _db.RoleDefinitions.Remove(role);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

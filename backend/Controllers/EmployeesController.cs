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
[Route("api/employees")]
public class EmployeesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public EmployeesController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    [HttpGet]
    [RequirePermission(Resources.Employees, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List(CancellationToken ct)
    {
        var items = await _db.Employees.AsNoTracking()
            .Where(e => e.TenantId == TenantId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new EmployeeDto(
                e.Id, e.Number, e.Code, e.Name, e.Mobile, e.Department, e.Designation,
                e.PlantId, e.Plant != null ? e.Plant.Name : null,
                e.ProcessId, e.Process != null ? e.Process.Name : null,
                e.IsActive, e.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(Resources.Employees, Actions.Add)]
    public async Task<ActionResult<EmployeeDto>> Create(EmployeeUpsertRequest req, CancellationToken ct)
    {
        if (req.PlantId is { } pid && !await _db.Plants.AnyAsync(p => p.Id == pid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Plant not found." });
        if (req.ProcessId is { } prid && !await _db.Processes.AnyAsync(p => p.Id == prid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Process not found." });

        var number = await _ng.NextEmployeeAsync(TenantId, ct);
        var emp = new Employee
        {
            TenantId = TenantId,
            Number = number,
            Code = INumberGenerator.FormatCode("EMP", number),
            Name = req.Name,
            Mobile = req.Mobile,
            Department = req.Department,
            Designation = req.Designation,
            PlantId = req.PlantId,
            ProcessId = req.ProcessId,
            IsActive = req.IsActive
        };
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync(ct);
        return await ToDto(emp.Id, ct);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Resources.Employees, Actions.Edit)]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, EmployeeUpsertRequest req, CancellationToken ct)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId, ct);
        if (emp is null) return NotFound();
        if (req.PlantId is { } pid && !await _db.Plants.AnyAsync(p => p.Id == pid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Plant not found." });
        if (req.ProcessId is { } prid && !await _db.Processes.AnyAsync(p => p.Id == prid && p.TenantId == TenantId, ct))
            return BadRequest(new { error = "Process not found." });

        // Code is immutable post-creation.
        emp.Name = req.Name;
        emp.Mobile = req.Mobile;
        emp.Department = req.Department;
        emp.Designation = req.Designation;
        emp.PlantId = req.PlantId;
        emp.ProcessId = req.ProcessId;
        emp.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return await ToDto(emp.Id, ct);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Employees, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId, ct);
        if (emp is null) return NotFound();
        _db.Employees.Remove(emp);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<EmployeeDto>> ToDto(Guid id, CancellationToken ct)
    {
        var dto = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDto(
                e.Id, e.Number, e.Code, e.Name, e.Mobile, e.Department, e.Designation,
                e.PlantId, e.Plant != null ? e.Plant.Name : null,
                e.ProcessId, e.Process != null ? e.Process.Name : null,
                e.IsActive, e.CreatedAtUtc))
            .FirstAsync(ct);
        return Ok(dto);
    }
}

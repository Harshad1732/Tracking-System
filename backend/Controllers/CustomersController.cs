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
[Route("api/customers")]
public class CustomersController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public CustomersController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    [HttpGet]
    [RequirePermission(Resources.Customers, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> List(CancellationToken ct)
    {
        var items = await _db.Customers.AsNoTracking()
            .Where(c => c.TenantId == TenantId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CustomerDto(c.Id, c.Number, c.Code, c.Name, c.ContactPerson, c.Mobile, c.Email, c.Address, c.IsActive, c.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission(Resources.Customers, Actions.Add)]
    public async Task<ActionResult<CustomerDto>> Create(CustomerUpsertRequest req, CancellationToken ct)
    {
        var number = await _ng.NextCustomerAsync(TenantId, ct);
        var item = new Customer
        {
            TenantId = TenantId,
            Number = number,
            // Auto-generated: CUS-001, CUS-002 ... — single source of truth.
            Code = INumberGenerator.FormatCode("CUS", number),
            Name = req.Name,
            ContactPerson = req.ContactPerson,
            Mobile = req.Mobile,
            Email = req.Email,
            Address = req.Address,
            IsActive = req.IsActive
        };
        _db.Customers.Add(item);
        await _db.SaveChangesAsync(ct);
        return Ok(new CustomerDto(item.Id, item.Number, item.Code, item.Name, item.ContactPerson, item.Mobile, item.Email, item.Address, item.IsActive, item.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Resources.Customers, Actions.Edit)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, CustomerUpsertRequest req, CancellationToken ct)
    {
        var item = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (item is null) return NotFound();

        // Code is auto-generated at create — immutable.
        item.Name = req.Name;
        item.ContactPerson = req.ContactPerson;
        item.Mobile = req.Mobile;
        item.Email = req.Email;
        item.Address = req.Address;
        item.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        return Ok(new CustomerDto(item.Id, item.Number, item.Code, item.Name, item.ContactPerson, item.Mobile, item.Email, item.Address, item.IsActive, item.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Customers, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        _db.Customers.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

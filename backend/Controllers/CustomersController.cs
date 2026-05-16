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
[Route("api/customers")]
public class CustomersController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public CustomersController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    [HttpGet]
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
    public async Task<ActionResult<CustomerDto>> Create(CustomerUpsertRequest req, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.TenantId == TenantId && c.Code == req.Code, ct))
            return Conflict(new { error = "A customer with this code already exists." });

        var item = new Customer
        {
            TenantId = TenantId,
            Number = await _ng.NextCustomerAsync(TenantId, ct),
            Code = req.Code,
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
    public async Task<ActionResult<CustomerDto>> Update(Guid id, CustomerUpsertRequest req, CancellationToken ct)
    {
        var item = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        if (await _db.Customers.AnyAsync(c => c.TenantId == TenantId && c.Code == req.Code && c.Id != id, ct))
            return Conflict(new { error = "A customer with this code already exists." });

        item.Code = req.Code;
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
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var item = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (item is null) return NotFound();
        _db.Customers.Remove(item);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

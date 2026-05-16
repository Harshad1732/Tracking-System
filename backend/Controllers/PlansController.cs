using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;

namespace Tracker.Controllers;

[ApiController]
[Route("api/plans")]
[AllowAnonymous]
public class PlansController : ControllerBase
{
    private readonly AppDbContext _db;
    public PlansController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> List(CancellationToken ct)
    {
        var items = await _db.Plans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanDto(
                p.Id, p.Code, p.Name, p.Description,
                p.MonthlyPriceCents, p.Currency,
                p.MaxSheets, p.MaxUsers, p.MaxShopfloors, p.RetentionDays,
                p.SortOrder, p.IsActive))
            .ToListAsync(ct);
        return Ok(items);
    }
}

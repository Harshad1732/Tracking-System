using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Services;

namespace Tracker.Controllers;

/// <summary>Full plan row with internal-only fields. Platform-admin endpoint.</summary>
public record PlanAdminDto(
    Guid Id, string Code, string Name, string? Description,
    int MonthlyPriceCents, string Currency,
    int MaxSheets, int MaxUsers, int MaxShopfloors, int RetentionDays,
    int TrialDays, int BillingIntervalMonths,
    bool IsDefaultOnSignup, bool IsActive,
    int SortOrder, string? StripePriceId);

[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPlanRegistry _plans;
    public PlansController(AppDbContext db, IPlanRegistry plans)
    {
        _db = db;
        _plans = plans;
    }

    // ----- Public listing (landing page pricing) -----
    [HttpGet]
    [AllowAnonymous]
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

    // ----- Platform-admin listing (full detail, includes internal flags) -----
    [HttpGet("admin")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<PlanAdminDto>>> ListAdmin(CancellationToken ct)
    {
        if (User.FindFirst(TrackerClaims.PlatformAdmin)?.Value != "true")
            return Forbid();

        var rows = await _plans.ListAsync(activeOnly: false, ct: ct);
        return Ok(rows.Select(p => new PlanAdminDto(
            p.Id, p.Code, p.Name, p.Description,
            p.MonthlyPriceCents, p.Currency,
            p.MaxSheets, p.MaxUsers, p.MaxShopfloors, p.RetentionDays,
            p.TrialDays, p.BillingIntervalMonths,
            p.IsDefaultOnSignup, p.IsActive,
            p.SortOrder, p.StripePriceId)).ToList());
    }
}

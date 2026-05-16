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
[Route("api/subscription")]
public class SubscriptionController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPlanLimitService _limits;
    public SubscriptionController(AppDbContext db, IPlanLimitService limits)
    {
        _db = db;
        _limits = limits;
    }

    [HttpGet("me")]
    public async Task<ActionResult<SubscriptionDto>> Me(CancellationToken ct)
    {
        var sub = await _db.Subscriptions.Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == TenantId, ct);
        if (sub is null) return NotFound();

        var sheetsUsed = await _db.GlassSheets.CountAsync(g => g.TenantId == TenantId, ct);
        var usersUsed  = await _db.Users.CountAsync(u => u.TenantId == TenantId, ct);
        var floorsUsed = await _db.Shopfloors.CountAsync(s => s.TenantId == TenantId, ct);

        var usage = new UsageDto(
            sheetsUsed, sub.Plan.MaxSheets,
            usersUsed,  sub.Plan.MaxUsers,
            floorsUsed, sub.Plan.MaxShopfloors);

        return Ok(new SubscriptionDto(
            sub.Id,
            new PlanDto(
                sub.Plan.Id, sub.Plan.Code, sub.Plan.Name, sub.Plan.Description,
                sub.Plan.MonthlyPriceCents, sub.Plan.Currency,
                sub.Plan.MaxSheets, sub.Plan.MaxUsers, sub.Plan.MaxShopfloors, sub.Plan.RetentionDays,
                sub.Plan.SortOrder, sub.Plan.IsActive),
            sub.Status, sub.TrialEndsAtUtc, sub.CurrentPeriodEndsAtUtc, sub.CanceledAtUtc,
            usage));
    }

    /// <summary>
    /// "Manual" upgrade path — moves the tenant to the chosen plan immediately.
    /// In production this should redirect to a Stripe checkout session; this implementation
    /// is suitable for sales demos and self-hosted deployments without a payment provider.
    /// </summary>
    [HttpPost("upgrade")]
    [RequirePermission(Resources.Workspace, Actions.Edit)]
    public async Task<ActionResult<SubscriptionDto>> Upgrade(UpgradePlanRequest req, CancellationToken ct)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == req.PlanCode && p.IsActive, ct);
        if (plan is null) return BadRequest(new { error = $"Plan '{req.PlanCode}' not found." });

        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == TenantId, ct);
        // Billing interval comes from the plan — single source of truth, no magic 1.
        var intervalMonths = plan.BillingIntervalMonths > 0 ? plan.BillingIntervalMonths : 1;
        if (sub is null)
        {
            sub = new Subscription
            {
                TenantId = TenantId,
                PlanId = plan.Id,
                Status = "Active",
                CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddMonths(intervalMonths)
            };
            _db.Subscriptions.Add(sub);
        }
        else
        {
            sub.PlanId = plan.Id;
            sub.Status = "Active";
            sub.TrialEndsAtUtc = null;
            sub.CanceledAtUtc = null;
            sub.CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddMonths(intervalMonths);
            sub.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return await Me(ct);
    }

    [HttpPost("cancel")]
    [RequirePermission(Resources.Workspace, Actions.Edit)]
    public async Task<ActionResult<SubscriptionDto>> Cancel(CancellationToken ct)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == TenantId, ct);
        if (sub is null) return NotFound();
        sub.Status = "Canceled";
        sub.CanceledAtUtc = DateTime.UtcNow;
        sub.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await Me(ct);
    }
}

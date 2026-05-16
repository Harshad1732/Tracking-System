using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Tracker.Services;

namespace Tracker.Controllers;

public abstract class TenantControllerBase : ControllerBase
{
    protected Guid TenantId
    {
        get
        {
            var raw = User.FindFirstValue(TrackerClaims.TenantId);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }

    /// <summary>
    /// Currently-selected plant context for this request, read from the JWT `pid` claim.
    /// Use this in WHERE clauses on any plant-scoped entity (Shopfloor, GlassSheet, Batch)
    /// so users only see data for the plant they're working in.
    /// </summary>
    protected Guid PlantId
    {
        get
        {
            var raw = User.FindFirstValue(TrackerClaims.PlantId);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}

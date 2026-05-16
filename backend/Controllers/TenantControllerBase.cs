using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Tracker.Controllers;

public abstract class TenantControllerBase : ControllerBase
{
    protected Guid TenantId
    {
        get
        {
            var raw = User.FindFirstValue("tid");
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Tracker.Services;

namespace Tracker.Filters;

/// <summary>
/// Controller-level guard that requires the caller to have a specific Resource+Action
/// permission. The codes are strings (validated against the DB at startup, see
/// <see cref="IPermissionSeeder.ValidateAttributeReferencesAsync"/>). Use the canonical
/// codes from <see cref="Resources"/> / <see cref="Actions"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string ResourceCode { get; }
    public string ActionCode { get; }

    public RequirePermissionAttribute(string resourceCode, string actionCode)
    {
        ResourceCode = resourceCode;
        ActionCode = actionCode;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var perms = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var allowed = await perms.HasAsync(ResourceCode, ActionCode, context.HttpContext.RequestAborted);
        if (!allowed)
        {
            context.Result = new ObjectResult(new
            {
                error = $"You don't have permission to {ActionCode.ToLowerInvariant()} {ResourceCode.ToLowerInvariant()}."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}

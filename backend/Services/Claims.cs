namespace Tracker.Services;

/// <summary>
/// Custom JWT claim names used across <see cref="TokenService"/>, <see cref="PermissionService"/>,
/// <see cref="Tracker.Controllers.TenantControllerBase"/>, and <see cref="Tracker.Controllers.PlatformController"/>.
///
/// Centralized so a rename is a single change and so unknown claims are easy to spot.
/// </summary>
public static class TrackerClaims
{
    /// <summary>Tenant id (Guid) that the access token is bound to.</summary>
    public const string TenantId = "tid";
    /// <summary>Tenant slug.</summary>
    public const string TenantSlug = "tslug";
    /// <summary>Tenant display name.</summary>
    public const string TenantName = "tname";
    /// <summary>Current plant id (Guid) — what controllers filter by.</summary>
    public const string PlantId = "pid";
    /// <summary>"true" if the user is a cross-tenant platform admin.</summary>
    public const string PlatformAdmin = "platform_admin";
}

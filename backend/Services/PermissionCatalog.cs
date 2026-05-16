namespace Tracker.Services;

/// <summary>
/// Canonical resource codes. Each constant maps 1:1 to a row in the PermResources table —
/// the DB row is the source of truth for the human-readable name and description; the
/// constant is just the stable identifier that controller attributes reference.
///
/// Adding a new resource: add a constant here AND a seed row in <see cref="PermissionSeeder"/>.
/// Startup validates that every constant exists in the DB.
/// </summary>
public static class Resources
{
    public const string Sheets     = "Sheets";
    public const string Batches    = "Batches";
    public const string Customers  = "Customers";
    public const string Employees  = "Employees";
    public const string Plants     = "Plants";
    public const string Shopfloors = "Shopfloors";
    public const string Processes  = "Processes";
    public const string Users      = "Users";
    public const string Roles      = "Roles";
    public const string Reports    = "Reports";
    public const string Workspace  = "Workspace";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Sheets, Batches, Customers, Employees, Plants, Shopfloors,
        Processes, Users, Roles, Reports, Workspace
    };
}

/// <summary>
/// Canonical action codes. Mirrors <see cref="Resources"/> — DB-driven values, constants
/// here exist only as compile-time identifiers for controller attributes.
/// </summary>
public static class Actions
{
    public const string View   = "View";
    public const string Add    = "Add";
    public const string Edit   = "Edit";
    public const string Delete = "Delete";

    public static readonly IReadOnlyList<string> All = new[] { View, Add, Edit, Delete };
}

/// <summary>
/// Canonical names of system roles seeded per tenant. NOT used for authorization checks
/// — those go through the permission matrix. These are referenced only by:
///   1. The seed service, to ensure the row exists per tenant.
///   2. The UI, to render a friendly label on the row.
/// Renaming a system role is intentionally allowed by an admin; the IsSystemAdmin /
/// IsSystem flags carry the contract.
/// </summary>
public static class SystemRoles
{
    public const string Admin    = "Admin";
    public const string Manager  = "Manager";
    public const string Operator = "Operator";
    public const string Viewer   = "Viewer";
}

/// <summary>Scope-type identifiers used in UserRoleAssignment.</summary>
public static class ScopeTypes
{
    public const string Tenant = "Tenant";
    public const string Plant  = "Plant";

    public static readonly IReadOnlyList<string> All = new[] { Tenant, Plant };
}

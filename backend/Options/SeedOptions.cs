namespace Tracker.Options;

/// <summary>
/// First-run seed configuration read from <c>appsettings.json</c> ("Seed" section).
/// In production these should be overridden via environment variables / Azure App Settings.
/// Lets you change the demo workspace identity without rebuilding.
/// </summary>
public class SeedOptions
{
    public DemoTenantOptions DemoTenant { get; set; } = new();
    public SeedUserOptions TenantAdmin { get; set; } = new();
    public SeedUserOptions PlatformAdmin { get; set; } = new();
}

public class DemoTenantOptions
{
    public string Slug { get; set; } = "demo";
    public string Name { get; set; } = "Demo Workspace";
}

public class SeedUserOptions
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Password { get; set; } = "";
}

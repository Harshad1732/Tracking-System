using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tracker.Data;
using Tracker.Entities;
using Tracker.Options;
using Tracker.Services;
using Tracker.Services.OAuth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection("Google"));
builder.Services.Configure<MicrosoftAuthOptions>(builder.Configuration.GetSection("Microsoft"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")
                      ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
var emailHost = builder.Configuration["Email:Host"];
if (!string.IsNullOrWhiteSpace(emailHost))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
builder.Services.AddSingleton<IGoogleAuthValidator, GoogleAuthValidator>();
builder.Services.AddSingleton<IMicrosoftAuthValidator, MicrosoftAuthValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPlanLimitService, PlanLimitService>();
builder.Services.AddScoped<INumberGenerator, NumberGenerator>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionSeeder, PermissionSeeder>();
builder.Services.AddScoped<IRoleAdminService, RoleAdminService>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<ISheetStatusService, SheetStatusService>();
builder.Services.AddScoped<IPlanRegistry, PlanRegistry>();
builder.Services.AddScoped<IApplicationLogger, ApplicationLogger>();
builder.Services.AddHttpContextAccessor();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddHealthChecks();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Catalog seed must run before everything else: the data migration in
    // 20260516120000_AddRbacMatrix relies on Resources/Actions/Permissions already
    // being present, so the migration handles them itself. This call is for any
    // newly-added resources/actions in future builds.
    var seeder = scope.ServiceProvider.GetRequiredService<IPermissionSeeder>();
    await seeder.SeedCatalogAsync();

    var seedOpts = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>().Value;

    await SeedPlansAsync(db);
    await SeedAdminAsync(db, scope.ServiceProvider, seeder, seedOpts);
    await SeedShopfloorsAsync(db, seedOpts);
    await SeedDemoSubscriptionAsync(db, seedOpts);

    // Self-check: every [RequirePermission(...)] reference must point at a catalog row.
    // Throws if a controller uses a code that wasn't seeded.
    await seeder.ValidateAttributeReferencesAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapHealthChecks("/healthz");

app.UseCors();
app.UseMiddleware<Tracker.Middleware.RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedAdminAsync(AppDbContext db, IServiceProvider sp, IPermissionSeeder seeder, SeedOptions opts)
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == opts.DemoTenant.Slug);
    if (tenant is null)
    {
        tenant = new Tenant { Name = opts.DemoTenant.Name, Slug = opts.DemoTenant.Slug };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
    }

    // Ensure the built-in roles exist for the demo tenant.
    await seeder.SeedBuiltInRolesAsync(tenant.Id);

    var adminRole = await db.RoleDefinitions
        .FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.IsSystemAdmin);
    if (adminRole is null) return;

    var hasher = sp.GetRequiredService<IPasswordHasher>();

    if (!string.IsNullOrWhiteSpace(opts.TenantAdmin.Email) &&
        !await db.Users.AnyAsync(u => u.TenantId == tenant.Id && u.Email == opts.TenantAdmin.Email))
    {
        var user = new User
        {
            TenantId = tenant.Id,
            Number = 1,
            Email = opts.TenantAdmin.Email,
            FullName = opts.TenantAdmin.FullName,
            PasswordHash = hasher.Hash(opts.TenantAdmin.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            RoleId = adminRole.Id,
            ScopeType = ScopeTypes.Tenant
        });
        await db.SaveChangesAsync();
    }

    if (!string.IsNullOrWhiteSpace(opts.PlatformAdmin.Email) &&
        !await db.Users.AnyAsync(u => u.TenantId == tenant.Id && u.Email == opts.PlatformAdmin.Email))
    {
        var maxNumber = await db.Users.Where(u => u.TenantId == tenant.Id)
            .Select(u => (int?)u.Number).MaxAsync() ?? 0;
        var user = new User
        {
            TenantId = tenant.Id,
            Number = maxNumber + 1,
            Email = opts.PlatformAdmin.Email,
            FullName = opts.PlatformAdmin.FullName,
            PasswordHash = hasher.Hash(opts.PlatformAdmin.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            RoleId = adminRole.Id,
            ScopeType = ScopeTypes.Tenant
        });
        db.PlatformAdmins.Add(new PlatformAdmin { UserId = user.Id });
        await db.SaveChangesAsync();
    }
}

static async Task SeedShopfloorsAsync(AppDbContext db, SeedOptions opts)
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == opts.DemoTenant.Slug);
    if (tenant is null) return;
    if (await db.Shopfloors.AnyAsync(s => s.TenantId == tenant.Id)) return;

    var plant = await db.Plants.FirstOrDefaultAsync(p => p.TenantId == tenant.Id);
    if (plant is null)
    {
        plant = new Plant
        {
            TenantId = tenant.Id, Number = 1, Code = "MAIN", Name = "Main Plant", IsActive = true
        };
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
    }

    var seeds = new[]
    {
        new Shopfloor { TenantId = tenant.Id, PlantId = plant.Id, Number = 1, Code = "STORAGE", Name = "Storage",      SequenceNo = 0,  IsStorage = true,  IsActive = true },
        new Shopfloor { TenantId = tenant.Id, PlantId = plant.Id, Number = 2, Code = "SF1",     Name = "Cutting",      SequenceNo = 10, IsStorage = false, IsActive = true },
        new Shopfloor { TenantId = tenant.Id, PlantId = plant.Id, Number = 3, Code = "SF2",     Name = "Edging",       SequenceNo = 20, IsStorage = false, IsActive = true },
        new Shopfloor { TenantId = tenant.Id, PlantId = plant.Id, Number = 4, Code = "SF3",     Name = "Marking",      SequenceNo = 30, IsStorage = false, IsActive = true },
        new Shopfloor { TenantId = tenant.Id, PlantId = plant.Id, Number = 5, Code = "SF4",     Name = "Blackborder",  SequenceNo = 40, IsStorage = false, IsActive = true }
    };
    db.Shopfloors.AddRange(seeds);
    await db.SaveChangesAsync();
}

static async Task SeedPlansAsync(AppDbContext db)
{
    if (await db.Plans.AnyAsync()) return;

    db.Plans.AddRange(
        // TrialDays/BillingIntervalMonths populated from migration backfill on existing rows.
        // IsDefaultOnSignup = true on exactly one plan — see IPlanRegistry.GetDefaultSignupPlanAsync.
        new Plan { Code = "free",     Name = "Free",       Description = "For evaluating Tracker on a small line.",
                   MonthlyPriceCents = 0,    Currency = "USD",
                   MaxSheets = 100,   MaxUsers = 2,  MaxShopfloors = 3,  RetentionDays = 30,  SortOrder = 10,
                   TrialDays = 14, BillingIntervalMonths = 1, IsDefaultOnSignup = true },
        new Plan { Code = "starter",  Name = "Starter",    Description = "For a single shopfloor running daily production.",
                   MonthlyPriceCents = 2900, Currency = "USD",
                   MaxSheets = 1000,  MaxUsers = 10, MaxShopfloors = 10, RetentionDays = 90,  SortOrder = 20,
                   TrialDays = 0, BillingIntervalMonths = 1 },
        new Plan { Code = "pro",      Name = "Pro",        Description = "For multi-line plants with QC and batching.",
                   MonthlyPriceCents = 9900, Currency = "USD",
                   MaxSheets = 10000, MaxUsers = 50, MaxShopfloors = 50, RetentionDays = 365, SortOrder = 30,
                   TrialDays = 0, BillingIntervalMonths = 1 },
        new Plan { Code = "enterprise", Name = "Enterprise", Description = "Custom limits, SSO, dedicated support.",
                   MonthlyPriceCents = 29900, Currency = "USD",
                   MaxSheets = 100000, MaxUsers = 500, MaxShopfloors = 500, RetentionDays = -1, SortOrder = 40,
                   TrialDays = 0, BillingIntervalMonths = 12 }
    );
    await db.SaveChangesAsync();
}

static async Task SeedDemoSubscriptionAsync(AppDbContext db, SeedOptions opts)
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == opts.DemoTenant.Slug);
    if (tenant is null) return;
    if (await db.Subscriptions.AnyAsync(s => s.TenantId == tenant.Id)) return;
    var proPlan = await db.Plans.FirstOrDefaultAsync(p => p.Code == "pro");
    if (proPlan is null) return;
    var months = proPlan.BillingIntervalMonths > 0 ? proPlan.BillingIntervalMonths : 12;
    db.Subscriptions.Add(new Subscription
    {
        TenantId = tenant.Id,
        PlanId = proPlan.Id,
        Status = "Active",
        // 12× billing intervals = roughly a year of evaluation runway for the demo tenant.
        CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddMonths(months * 12)
    });
    await db.SaveChangesAsync();
}

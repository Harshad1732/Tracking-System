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
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    // HS256 requires >=256 bits of key material. An empty/short key would either crash at
    // token-issue time or (worse) ship a weak signing key. Fail fast at startup instead.
    // Set via `dotnet user-secrets set "Jwt:Key" "<64+ random chars>"` locally,
    // or App Setting `Jwt__Key` (ideally a Key Vault reference) on Azure. See SECRETS.md.
    throw new InvalidOperationException(
        "Jwt:Key is not configured or is shorter than 32 chars. See SECRETS.md.");
}
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

    // Require BOTH email and password so we never create a seed admin with an empty
    // password hash (which would still satisfy non-empty checks but never authenticate).
    // Configure Seed:TenantAdmin:Password via user-secrets locally / App Settings on Azure.
    if (!string.IsNullOrWhiteSpace(opts.TenantAdmin.Email) &&
        !string.IsNullOrWhiteSpace(opts.TenantAdmin.Password) &&
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
        !string.IsNullOrWhiteSpace(opts.PlatformAdmin.Password) &&
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

    // INR-tier offering: three plans, all uncapped on sheets/users/floors. Customers
    // pick based on commitment length, not feature gating. Cents-named field is the
    // smallest currency unit — for INR that's paise (1 INR = 100 paise).
    db.Plans.AddRange(
        new Plan { Code = "annual", Name = "Annual",
                   Description = "1-year commitment. ₹4,00,000 billed up front.",
                   MonthlyPriceCents = 3_333_333, Currency = "INR",
                   MaxSheets = int.MaxValue, MaxUsers = int.MaxValue, MaxShopfloors = int.MaxValue,
                   RetentionDays = -1, SortOrder = 10,
                   TrialDays = 0, BillingIntervalMonths = 12, IsDefaultOnSignup = true },
        new Plan { Code = "biennial", Name = "Biennial",
                   Description = "2-year commitment. ₹7,00,000 billed up front — saves ₹1,00,000 vs annual.",
                   MonthlyPriceCents = 2_916_667, Currency = "INR",
                   MaxSheets = int.MaxValue, MaxUsers = int.MaxValue, MaxShopfloors = int.MaxValue,
                   RetentionDays = -1, SortOrder = 20,
                   TrialDays = 0, BillingIntervalMonths = 24 },
        new Plan { Code = "unlimited", Name = "Unlimited",
                   Description = "Pay-as-you-go monthly. ₹20,000 / month, cancel any time.",
                   MonthlyPriceCents = 2_000_000, Currency = "INR",
                   MaxSheets = int.MaxValue, MaxUsers = int.MaxValue, MaxShopfloors = int.MaxValue,
                   RetentionDays = -1, SortOrder = 30,
                   TrialDays = 0, BillingIntervalMonths = 1 }
    );
    await db.SaveChangesAsync();
}

static async Task SeedDemoSubscriptionAsync(AppDbContext db, SeedOptions opts)
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == opts.DemoTenant.Slug);
    if (tenant is null) return;
    if (await db.Subscriptions.AnyAsync(s => s.TenantId == tenant.Id)) return;
    // Pick the signup-default plan rather than a hardcoded code — keeps the seed in sync
    // when the catalog gets renamed/retiered without code edits here.
    var plan = await db.Plans.FirstOrDefaultAsync(p => p.IsDefaultOnSignup && p.IsActive)
            ?? await db.Plans.OrderBy(p => p.SortOrder).FirstOrDefaultAsync();
    if (plan is null) return;
    var months = plan.BillingIntervalMonths > 0 ? plan.BillingIntervalMonths : 12;
    db.Subscriptions.Add(new Subscription
    {
        TenantId = tenant.Id,
        PlanId = plan.Id,
        Status = "Active",
        // 12× billing intervals = roughly a year of evaluation runway for the demo tenant.
        CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddMonths(months * 12)
    });
    await db.SaveChangesAsync();
}

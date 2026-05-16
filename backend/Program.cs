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

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Keep claim names exactly as issued (preserves "tid", "tslug", etc.).
        // Without this, the JWT handler rewrites short claim types to Microsoft URIs.
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

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Applies any pending EF Core migrations (and creates the DB if missing).
    // Run `dotnet ef migrations add <Name>` from the backend folder when entities change.
    db.Database.Migrate();
    await SeedPlansAsync(db);
    await SeedAdminAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
    await SeedShopfloorsAsync(db);
    await SeedDemoSubscriptionAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedAdminAsync(AppDbContext db, IPasswordHasher hasher)
{
    const string defaultSlug = "demo";
    const string adminEmail = "admin@tracker.local";

    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == defaultSlug);
    if (tenant is null)
    {
        tenant = new Tenant { Name = "Demo Workspace", Slug = defaultSlug };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
    }

    if (await db.Users.AnyAsync(u => u.TenantId == tenant.Id && u.Email == adminEmail)) return;
    db.Users.Add(new User
    {
        TenantId = tenant.Id,
        Number = 1,
        Email = adminEmail,
        FullName = "Tracker Admin",
        PasswordHash = hasher.Hash("Admin#12345"),
        Role = "Admin"
    });
    await db.SaveChangesAsync();
}

static async Task SeedShopfloorsAsync(AppDbContext db)
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "demo");
    if (tenant is null) return;
    if (await db.Shopfloors.AnyAsync(s => s.TenantId == tenant.Id)) return;

    var seeds = new[]
    {
        new Shopfloor { TenantId = tenant.Id, Number = 1, Code = "STORAGE", Name = "Storage",      SequenceNo = 0,  IsStorage = true,  IsActive = true },
        new Shopfloor { TenantId = tenant.Id, Number = 2, Code = "SF1",     Name = "Cutting",      SequenceNo = 10, IsStorage = false, IsActive = true },
        new Shopfloor { TenantId = tenant.Id, Number = 3, Code = "SF2",     Name = "Edging",       SequenceNo = 20, IsStorage = false, IsActive = true },
        new Shopfloor { TenantId = tenant.Id, Number = 4, Code = "SF3",     Name = "Marking",      SequenceNo = 30, IsStorage = false, IsActive = true },
        new Shopfloor { TenantId = tenant.Id, Number = 5, Code = "SF4",     Name = "Blackborder",  SequenceNo = 40, IsStorage = false, IsActive = true }
    };
    db.Shopfloors.AddRange(seeds);
    await db.SaveChangesAsync();
}

static async Task SeedPlansAsync(AppDbContext db)
{
    if (await db.Plans.AnyAsync()) return;

    db.Plans.AddRange(
        new Plan { Code = "free",     Name = "Free",       Description = "For evaluating Tracker on a small line.",
                   MonthlyPriceCents = 0,    Currency = "USD",
                   MaxSheets = 100,   MaxUsers = 2,  MaxShopfloors = 3,  RetentionDays = 30,  SortOrder = 10 },
        new Plan { Code = "starter",  Name = "Starter",    Description = "For a single shopfloor running daily production.",
                   MonthlyPriceCents = 2900, Currency = "USD",
                   MaxSheets = 1000,  MaxUsers = 10, MaxShopfloors = 10, RetentionDays = 90,  SortOrder = 20 },
        new Plan { Code = "pro",      Name = "Pro",        Description = "For multi-line plants with QC and batching.",
                   MonthlyPriceCents = 9900, Currency = "USD",
                   MaxSheets = 10000, MaxUsers = 50, MaxShopfloors = 50, RetentionDays = 365, SortOrder = 30 },
        new Plan { Code = "enterprise", Name = "Enterprise", Description = "Custom limits, SSO, dedicated support.",
                   MonthlyPriceCents = 29900, Currency = "USD",
                   MaxSheets = 100000, MaxUsers = 500, MaxShopfloors = 500, RetentionDays = -1, SortOrder = 40 }
    );
    await db.SaveChangesAsync();
}

static async Task SeedDemoSubscriptionAsync(AppDbContext db)
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "demo");
    if (tenant is null) return;
    if (await db.Subscriptions.AnyAsync(s => s.TenantId == tenant.Id)) return;
    var proPlan = await db.Plans.FirstOrDefaultAsync(p => p.Code == "pro");
    if (proPlan is null) return;
    db.Subscriptions.Add(new Subscription
    {
        TenantId = tenant.Id,
        PlanId = proPlan.Id,
        Status = "Active",
        CurrentPeriodEndsAtUtc = DateTime.UtcNow.AddYears(1)
    });
    await db.SaveChangesAsync();
}

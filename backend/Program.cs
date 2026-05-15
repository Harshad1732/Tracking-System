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

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default")
                   ?? "Data Source=tracker.db"));

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
builder.Services.AddSingleton<IGoogleAuthValidator, GoogleAuthValidator>();
builder.Services.AddSingleton<IMicrosoftAuthValidator, MicrosoftAuthValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
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
    .AllowAnyMethod()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await SeedAdminAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
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
        Email = adminEmail,
        FullName = "Tracker Admin",
        PasswordHash = hasher.Hash("Admin#12345"),
        Role = "Admin"
    });
    await db.SaveChangesAsync();
}

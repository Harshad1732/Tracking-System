using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
using Tracker.Options;
using Tracker.Services.OAuth;

namespace Tracker.Services;

public interface IAuthService
{
    Task<(AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest req, CancellationToken ct);
    Task<AuthResponse?> LoginAsync(LoginRequest req, CancellationToken ct);
    Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct);
    Task LogoutAsync(string refreshToken, CancellationToken ct);
    Task ForgotPasswordAsync(string tenantSlug, string email, string resetUrlBase, CancellationToken ct);
    Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken ct);
    Task<AuthResponse?> GoogleLoginAsync(string tenantSlug, string idToken, CancellationToken ct);
    Task<AuthResponse?> MicrosoftLoginAsync(string tenantSlug, string idToken, CancellationToken ct);
    Task<AuthResponse> IssueTokensForTenantAsync(User user, Tenant targetTenant, CancellationToken ct);
    Task<AuthResponse> IssueTokensForPlantAsync(User user, Tenant tenant, Guid plantId, CancellationToken ct);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IGoogleAuthValidator _google;
    private readonly IMicrosoftAuthValidator _microsoft;
    private readonly IPermissionService _perms;
    private readonly IPermissionSeeder _seeder;
    private readonly IPlanRegistry _plans;
    private readonly JwtOptions _jwt;

    public AuthService(
        AppDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        IEmailSender email,
        IGoogleAuthValidator google,
        IMicrosoftAuthValidator microsoft,
        IPermissionService perms,
        IPermissionSeeder seeder,
        IPlanRegistry plans,
        IOptions<JwtOptions> jwt)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _email = email;
        _google = google;
        _microsoft = microsoft;
        _perms = perms;
        _seeder = seeder;
        _plans = plans;
        _jwt = jwt.Value;
    }

    public async Task<(AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var baseSlug = Slugify(req.TenantName);
        if (string.IsNullOrWhiteSpace(baseSlug))
            return (null, "Invalid workspace name.");

        var slug = baseSlug;
        var suffix = 1;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
            if (suffix > 100) return (null, "Could not generate a unique workspace URL.");
        }

        var tenant = new Tenant { Name = req.TenantName.Trim(), Slug = slug };
        _db.Tenants.Add(tenant);

        var mainPlant = new Plant
        {
            TenantId = tenant.Id,
            Number = 1,
            Code = "MAIN",
            Name = "Main Plant",
            IsActive = true
        };
        _db.Plants.Add(mainPlant);

        // Every sheet entry point (single/bulk/replacement) requires an active IsStorage
        // shopfloor on the current plant. Seed one here so a fresh tenant can add sheets
        // immediately. Production floors are intentionally left to the admin — those vary
        // by business and shouldn't be opinionated by us.
        _db.Shopfloors.Add(new Shopfloor
        {
            TenantId = tenant.Id,
            PlantId = mainPlant.Id,
            Number = 1,
            Code = "STORAGE",
            Name = "Storage",
            SequenceNo = 0,
            IsStorage = true,
            BatchMode = "None",
            IsActive = true
        });

        var user = new User
        {
            TenantId = tenant.Id,
            Number = 1,
            Email = email,
            FullName = req.FullName,
            PasswordHash = _hasher.Hash(req.Password)
        };
        _db.Users.Add(user);

        var signupPlan = await _plans.GetDefaultSignupPlanAsync(ct);
        if (signupPlan is not null)
        {
            // Trial length is plan-driven (Plan.TrialDays). 0 = no trial — straight to Active.
            var trialDays = signupPlan.TrialDays;
            var now = DateTime.UtcNow;
            _db.Subscriptions.Add(new Subscription
            {
                TenantId = tenant.Id,
                PlanId = signupPlan.Id,
                Status = trialDays > 0 ? "Trial" : "Active",
                TrialEndsAtUtc = trialDays > 0 ? now.AddDays(trialDays) : null,
                CurrentPeriodEndsAtUtc = trialDays > 0
                    ? now.AddDays(trialDays)
                    : now.AddMonths(signupPlan.BillingIntervalMonths)
            });
        }

        await _db.SaveChangesAsync(ct);

        // Seed the 4 built-in roles for the brand-new tenant, then grant the registrant
        // a tenant-scoped Admin assignment so they can actually use the workspace.
        await _seeder.SeedBuiltInRolesAsync(tenant.Id, ct);

        var adminRole = await _db.RoleDefinitions
            .FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.IsSystemAdmin, ct);
        if (adminRole is null)
            return (null, "Failed to seed the workspace admin role.");

        _db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            RoleId = adminRole.Id,
            ScopeType = ScopeTypes.Tenant
        });
        await _db.SaveChangesAsync(ct);

        return (await IssueTokensAsync(user, tenant, ct), null);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var (tenant, user) = await ResolveUserAsync(req.TenantSlug, req.Email, ct);
        if (tenant is null || user is null || user.PasswordHash is null) return null;
        if (!_hasher.Verify(req.Password, user.PasswordHash)) return null;
        return await IssueTokensAsync(user, tenant, ct);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive) return null;

        stored.RevokedAtUtc = DateTime.UtcNow;
        var response = await IssueTokensAsync(stored.User, stored.User.Tenant, ct, stored);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.IsActive)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(string tenantSlug, string email, string resetUrlBase, CancellationToken ct)
    {
        var (_, user) = await ResolveUserAsync(tenantSlug, email, ct);
        if (user is null) return;

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync(ct);

        var link = $"{resetUrlBase.TrimEnd('/')}/reset-password?token={rawToken}";
        await _email.SendAsync(user.Email,
            "Reset your Tracker password",
            $"Click here to reset your password (valid for 1 hour):\n{link}", ct);
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken ct)
    {
        var hash = _tokens.HashToken(token);
        var record = await _db.PasswordResetTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (record is null || record.UsedAtUtc is not null || record.ExpiresAtUtc < DateTime.UtcNow)
            return false;

        record.User.PasswordHash = _hasher.Hash(newPassword);
        record.UsedAtUtc = DateTime.UtcNow;

        var activeRefresh = await _db.RefreshTokens
            .Where(t => t.UserId == record.UserId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefresh) rt.RevokedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<AuthResponse?> GoogleLoginAsync(string tenantSlug, string idToken, CancellationToken ct) =>
        ExternalLoginAsync(tenantSlug, "google", _google.ValidateAsync(idToken, ct), ct);

    public Task<AuthResponse?> MicrosoftLoginAsync(string tenantSlug, string idToken, CancellationToken ct) =>
        ExternalLoginAsync(tenantSlug, "microsoft", _microsoft.ValidateAsync(idToken, ct), ct);

    private async Task<AuthResponse?> ExternalLoginAsync(
        string tenantSlug, string provider, Task<ExternalUserInfo?> validation, CancellationToken ct)
    {
        var info = await validation;
        if (info is null) return null;

        var slug = tenantSlug.Trim().ToLowerInvariant();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
        if (tenant is null) return null;

        var email = info.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.TenantId == tenant.Id &&
            (u.Email == email || (u.Provider == provider && u.ProviderUserId == info.ProviderUserId)), ct);

        if (user is null)
        {
            user = new User
            {
                TenantId = tenant.Id,
                Email = email,
                FullName = info.Name,
                Provider = provider,
                ProviderUserId = info.ProviderUserId
            };
            _db.Users.Add(user);
        }
        else if (user.Provider is null)
        {
            user.Provider = provider;
            user.ProviderUserId = info.ProviderUserId;
            if (string.IsNullOrWhiteSpace(user.FullName)) user.FullName = info.Name;
        }

        await _db.SaveChangesAsync(ct);
        return await IssueTokensAsync(user, tenant, ct);
    }

    private async Task<(Tenant? Tenant, User? User)> ResolveUserAsync(
        string tenantSlug, string email, CancellationToken ct)
    {
        var slug = tenantSlug.Trim().ToLowerInvariant();
        var emailNorm = email.Trim().ToLowerInvariant();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
        if (tenant is null) return (null, null);
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenant.Id && u.Email == emailNorm, ct);
        return (tenant, user);
    }

    private Task<AuthResponse> IssueTokensAsync(
        User user, Tenant tenant, CancellationToken ct, RefreshToken? replacing = null)
        => IssueTokensInternalAsync(user, tenant, null, ct, replacing);

    public Task<AuthResponse> IssueTokensForTenantAsync(User user, Tenant targetTenant, CancellationToken ct)
        => IssueTokensInternalAsync(user, targetTenant, null, ct, null);

    public Task<AuthResponse> IssueTokensForPlantAsync(User user, Tenant tenant, Guid plantId, CancellationToken ct)
        => IssueTokensInternalAsync(user, tenant, plantId, ct, null);

    private async Task<AuthResponse> IssueTokensInternalAsync(
        User user, Tenant tenant, Guid? targetPlantId, CancellationToken ct, RefreshToken? replacing)
    {
        var plantId = targetPlantId
            ?? user.PlantId
            ?? await _db.Plants.AsNoTracking()
                   .Where(p => p.TenantId == tenant.Id && p.IsActive)
                   .OrderBy(p => p.Number)
                   .Select(p => (Guid?)p.Id)
                   .FirstOrDefaultAsync(ct)
            ?? await _db.Plants.AsNoTracking()
                   .Where(p => p.TenantId == tenant.Id)
                   .OrderBy(p => p.Number)
                   .Select(p => p.Id)
                   .FirstAsync(ct);

        var isPlatformAdmin = await _db.PlatformAdmins.AnyAsync(pa => pa.UserId == user.Id, ct);

        var (access, accessExp) = _tokens.CreateAccessToken(user, tenant, plantId, isPlatformAdmin);
        var (refresh, refreshHash, refreshExp) = _tokens.CreateRefreshToken();

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAtUtc = refreshExp
        };
        _db.RefreshTokens.Add(entity);
        if (replacing is not null) replacing.ReplacedByTokenId = entity.Id;
        await _db.SaveChangesAsync(ct);

        EffectivePermissions eff = isPlatformAdmin
            ? EffectivePermissions.Platform
            : await _perms.ResolveAsync(user.Id, tenant.Id, plantId, ct);

        var userDto = new UserDto(
            user.Id, user.Email, user.FullName,
            eff.RoleNames,
            eff.IsSystemAdmin,
            isPlatformAdmin,
            eff.Grants.Select(g => new PermissionGrantDto(g.Resource, g.Action)).ToList(),
            user.PlantId,
            plantId);

        return new AuthResponse(
            access, refresh, accessExp,
            userDto,
            new TenantDto(tenant.Id, tenant.Name, tenant.Slug));
    }

    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9\s-]", "");
        s = Regex.Replace(s, @"[\s-]+", "-").Trim('-');
        if (s.Length > 60) s = s[..60].Trim('-');
        return s;
    }
}

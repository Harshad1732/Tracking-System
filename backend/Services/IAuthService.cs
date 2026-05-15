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
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IGoogleAuthValidator _google;
    private readonly IMicrosoftAuthValidator _microsoft;
    private readonly JwtOptions _jwt;

    public AuthService(
        AppDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        IEmailSender email,
        IGoogleAuthValidator google,
        IMicrosoftAuthValidator microsoft,
        IOptions<JwtOptions> jwt)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _email = email;
        _google = google;
        _microsoft = microsoft;
        _jwt = jwt.Value;
    }

    public async Task<(AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var slug = Slugify(req.TenantName);
        if (string.IsNullOrWhiteSpace(slug))
            return (null, "Invalid workspace name.");

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            return (null, "Workspace slug already exists. Pick a different workspace name.");

        var tenant = new Tenant { Name = req.TenantName.Trim(), Slug = slug };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            FullName = req.FullName,
            PasswordHash = _hasher.Hash(req.Password),
            Role = "Admin"
        };
        _db.Users.Add(user);
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
                Role = "User",
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

    private async Task<AuthResponse> IssueTokensAsync(
        User user, Tenant tenant, CancellationToken ct, RefreshToken? replacing = null)
    {
        var (access, accessExp) = _tokens.CreateAccessToken(user, tenant);
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

        return new AuthResponse(
            access, refresh, accessExp,
            new UserDto(user.Id, user.Email, user.FullName, user.Role),
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

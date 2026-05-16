using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? FullName,
    [Required, MaxLength(120)] string TenantName);

public record LoginRequest(
    [Required] string TenantSlug,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record LogoutRequest([Required] string RefreshToken);

public record ForgotPasswordRequest(
    [Required] string TenantSlug,
    [Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

public record GoogleLoginRequest(
    [Required] string TenantSlug,
    [Required] string IdToken);

public record MicrosoftLoginRequest(
    [Required] string TenantSlug,
    [Required] string IdToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    UserDto User,
    TenantDto Tenant);

/// <summary>
/// User shape returned to the client. Replaces the legacy single-string Role + 5-bool
/// PermissionDto with the union of grants from every active role assignment.
/// </summary>
public record UserDto(
    Guid Id,
    string Email,
    string? FullName,
    IReadOnlyList<string> Roles,
    bool IsSystemAdmin,
    bool IsPlatformAdmin,
    IReadOnlyList<PermissionGrantDto> Permissions,
    Guid? LockedPlantId,
    Guid CurrentPlantId);

/// <summary>One (resource, action) pair the caller can perform in the current context.</summary>
public record PermissionGrantDto(string Resource, string Action);

public record TenantDto(Guid Id, string Name, string Slug);

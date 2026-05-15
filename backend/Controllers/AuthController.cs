using Microsoft.AspNetCore.Mvc;
using Tracker.Dtos;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
    {
        var (result, error) = await _auth.RegisterAsync(req, ct);
        return result is null ? Conflict(new { error }) : Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(req, ct);
        return result is null
            ? Unauthorized(new { error = "Invalid workspace, email, or password." })
            : Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(req.RefreshToken, ct);
        return result is null ? Unauthorized(new { error = "Invalid or expired refresh token." }) : Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req, CancellationToken ct)
    {
        var origin = Request.Headers.Origin.FirstOrDefault() ?? "http://localhost:4200";
        await _auth.ForgotPasswordAsync(req.TenantSlug, req.Email, origin, ct);
        return Ok(new { message = "If the email is registered in that workspace, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req, CancellationToken ct)
    {
        var ok = await _auth.ResetPasswordAsync(req.Token, req.NewPassword, ct);
        return ok ? Ok(new { message = "Password reset." })
                  : BadRequest(new { error = "Invalid or expired reset token." });
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleLoginRequest req, CancellationToken ct)
    {
        var result = await _auth.GoogleLoginAsync(req.TenantSlug, req.IdToken, ct);
        return result is null ? Unauthorized(new { error = "Invalid Google token or workspace." }) : Ok(result);
    }

    [HttpPost("microsoft")]
    public async Task<ActionResult<AuthResponse>> Microsoft(MicrosoftLoginRequest req, CancellationToken ct)
    {
        var result = await _auth.MicrosoftLoginAsync(req.TenantSlug, req.IdToken, ct);
        return result is null ? Unauthorized(new { error = "Invalid Microsoft token or workspace." }) : Ok(result);
    }
}

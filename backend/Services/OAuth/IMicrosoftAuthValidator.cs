using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Tracker.Options;

namespace Tracker.Services.OAuth;

public interface IMicrosoftAuthValidator
{
    Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default);
}

public class MicrosoftAuthValidator : IMicrosoftAuthValidator
{
    private readonly MicrosoftAuthOptions _opts;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public MicrosoftAuthValidator(IOptions<MicrosoftAuthOptions> opts)
    {
        _opts = opts.Value;
        var authority = $"https://login.microsoftonline.com/{_opts.Tenant}/v2.0";
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            authority + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ClientId)) return null;
        try
        {
            var config = await _configManager.GetConfigurationAsync(ct);
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = true,
                ValidAudience = _opts.ClientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = config.SigningKeys,
                ValidateLifetime = true
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(idToken, validationParams, out _);

            var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub")
                      ?? principal.FindFirstValue("oid");
            var email = principal.FindFirstValue(ClaimTypes.Email)
                        ?? principal.FindFirstValue("email")
                        ?? principal.FindFirstValue("preferred_username");
            var name = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email)) return null;
            return new ExternalUserInfo(sub, email, name);
        }
        catch
        {
            return null;
        }
    }
}

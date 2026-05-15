using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Tracker.Options;

namespace Tracker.Services.OAuth;

public record ExternalUserInfo(string ProviderUserId, string Email, string? Name);

public interface IGoogleAuthValidator
{
    Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default);
}

public class GoogleAuthValidator : IGoogleAuthValidator
{
    private readonly GoogleAuthOptions _opts;

    public GoogleAuthValidator(IOptions<GoogleAuthOptions> opts) => _opts = opts.Value;

    public async Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ClientId)) return null;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _opts.ClientId }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (string.IsNullOrWhiteSpace(payload.Email)) return null;
            return new ExternalUserInfo(payload.Subject, payload.Email, payload.Name);
        }
        catch
        {
            return null;
        }
    }
}

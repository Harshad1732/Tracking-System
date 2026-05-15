namespace Tracker.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "Tracker";
    public string Audience { get; set; } = "TrackerClient";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
}

public class MicrosoftAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string Tenant { get; set; } = "common";
}

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

public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Tracker";
}

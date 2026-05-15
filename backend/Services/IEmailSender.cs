namespace Tracker.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("=== EMAIL (dev stub) ===\nTo: {Email}\nSubject: {Subject}\n{Body}\n========================",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}

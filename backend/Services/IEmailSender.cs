using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Tracker.Options;

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
        _logger.LogInformation("=== EMAIL (dev stub — SMTP not configured) ===\nTo: {Email}\nSubject: {Subject}\n{Body}\n========================",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.Username, _options.Password)
        };

        var fromAddress = string.IsNullOrWhiteSpace(_options.FromAddress)
            ? _options.Username
            : _options.FromAddress;

        using var msg = new MailMessage
        {
            From = new MailAddress(fromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        msg.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email sent to {To} via {Host}:{Port}", toEmail, _options.Host, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} via {Host}:{Port}", toEmail, _options.Host, _options.Port);
            throw;
        }
    }
}

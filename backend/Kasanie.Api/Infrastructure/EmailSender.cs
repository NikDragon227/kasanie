using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Kasanie.Api.Infrastructure;

public sealed class EmailOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string From { get; init; } = "no-reply@kasanie.local";
    public bool UseSsl { get; init; } = true;
}

public interface ITransactionalEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

public sealed class TransactionalEmailSender(IOptions<EmailOptions> options, IWebHostEnvironment environment, ILogger<TransactionalEmailSender> logger) : ITransactionalEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(settings.Host))
        {
            logger.LogInformation("Development email to {Recipient}: {Subject}\n{Body}", recipient, subject, body);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("SMTP_HOST must be configured outside Development.");

        using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.UseSsl };
        if (!string.IsNullOrWhiteSpace(settings.Username)) client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        using var message = new MailMessage(settings.From, recipient, subject, body) { IsBodyHtml = false };
        await client.SendMailAsync(message, cancellationToken);
    }
}

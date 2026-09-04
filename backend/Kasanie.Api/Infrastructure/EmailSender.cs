using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

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

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        // 465 — implicit TLS; 587/иные — STARTTLS (обязателен, если UseSsl). System.Net.Mail.SmtpClient
        // договаривался о STARTTLS ненадёжно и мог уйти в отправку без AUTH — MailKit делает это корректно.
        var security = settings.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.StartTlsWhenAvailable;

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

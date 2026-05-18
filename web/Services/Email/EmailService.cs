using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace web.Services.Email;

public sealed class EmailService(IOptions<EmailSettings> options) : IEmailService
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendEmailAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient();
        var secureSocketOptions = _settings.Smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(_settings.Smtp.Host, _settings.Smtp.Port, secureSocketOptions, cancellationToken);
        await client.AuthenticateAsync(_settings.Smtp.Username, _settings.Smtp.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string? toName = null, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.Smtp.FromName ?? _settings.Smtp.FromEmail, _settings.Smtp.FromEmail));
        message.To.Add(new MailboxAddress(toName ?? toEmail, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        await SendEmailAsync(message, cancellationToken);
    }
}

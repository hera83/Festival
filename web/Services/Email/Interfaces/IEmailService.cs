using MimeKit;

namespace web.Services.Email;

public interface IEmailService
{
    Task SendEmailAsync(MimeMessage message, CancellationToken cancellationToken = default);
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, string? toName = null, CancellationToken cancellationToken = default);
}

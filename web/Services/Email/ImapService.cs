using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace web.Services.Email;

public sealed class ImapService(IOptions<EmailSettings> options) : IImapService
{
    private readonly EmailSettings _settings = options.Value;

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        using var client = new ImapClient();
        var secureSocketOptions = _settings.Imap.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(_settings.Imap.Host, _settings.Imap.Port, secureSocketOptions, cancellationToken);
        await client.AuthenticateAsync(_settings.Imap.Username, _settings.Imap.Password, cancellationToken);
        var connected = client.IsConnected && client.IsAuthenticated;
        await client.DisconnectAsync(true, cancellationToken);

        return connected;
    }
}

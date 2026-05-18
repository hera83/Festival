namespace web.Services.Email;

public interface IImapService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}

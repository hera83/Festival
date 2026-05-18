namespace web.Services.Email;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public required SmtpSettings Smtp { get; init; }
    public required ImapSettings Imap { get; init; }

    public sealed class SmtpSettings
    {
        public required string Host { get; init; }
        public int Port { get; init; } = 465;
        public bool UseSsl { get; init; } = true;
        public required string Username { get; init; }
        public required string Password { get; init; }
        public required string FromEmail { get; init; }
        public string? FromName { get; init; }
    }

    public sealed class ImapSettings
    {
        public required string Host { get; init; }
        public int Port { get; init; } = 993;
        public bool UseSsl { get; init; } = true;
        public required string Username { get; init; }
        public required string Password { get; init; }
    }
}

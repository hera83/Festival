using System;

namespace web.Models;

public class MessageReply
{
    public int Id { get; set; }

    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;

    // Hvem sendte svaret – koordinator-bruger-id eller null hvis det er den frivillige
    public string? SentByUserId { get; set; }

    // Retning af svaret
    public MessageDirection Direction { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

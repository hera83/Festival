using System;

namespace web.Models;

public class MessageTask
{
    public int Id { get; set; }

    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public MessageTaskStatus Status { get; set; } = MessageTaskStatus.Åben;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
}

public enum MessageTaskStatus
{
    Åben = 0,
    IGang = 1,
    Udført = 2
}

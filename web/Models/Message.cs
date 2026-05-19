using System;
using System.Collections.Generic;

namespace web.Models;

public class Message
{
    public int Id { get; set; }
    public int SeasonId { get; set; }

    // Den frivillige som beskeden vedrører
    public int VolunteerId { get; set; }
    public Volunteer Volunteer { get; set; } = null!;

    // Hvem sendte beskeden (bruger-id fra AspNetUsers)
    public string SentByUserId { get; set; } = string.Empty;

    // Retning: Inbound = frivillig → koordinator, Outbound = koordinator → frivillig
    public MessageDirection Direction { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.Now;
    public DateTime? ReadAt { get; set; }

    // Soft delete – bruges når der er tilknyttede opgaver
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Hvornår den frivillige sidst åbnede tråden (bruges til ulæst-logik i appen)
    public DateTime? VolunteerOpenedAt { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    public ICollection<MessageTask> Tasks { get; set; } = new List<MessageTask>();
    public ICollection<MessageReply> Replies { get; set; } = new List<MessageReply>();
}

public enum MessageDirection
{
    Inbound = 0,   // Fra frivillig
    Outbound = 1   // Fra koordinator
}

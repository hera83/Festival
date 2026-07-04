using System;
using System.Collections.Generic;

namespace web.Models;

// ── Index ViewModel ──────────────────────────────────────────────
public class BeskederIndexViewModel
{
    public int UnreadCount  { get; set; }
    public int ReadCount    { get; set; }
    public int TaskCount    { get; set; }
}

// ── Kanal for en tråd i Besked Center ────────────────────────────
public enum ThreadChannel
{
    App = 0,
    Sms = 1
}

// ── Samlet tråd-række (app-besked eller sms-samtale) ─────────────
public class ThreadRowViewModel
{
    public ThreadChannel Channel   { get; set; }
    public int?    MessageId       { get; set; }   // sat for App
    public int?    VolunteerId     { get; set; }   // sat for Sms med kendt frivillig
    public string? PhoneNumber     { get; set; }   // sat for Sms uden kendt frivillig ("Ukendt")
    public string  VolunteerName   { get; set; } = string.Empty;
    public string  VolunteerKey    { get; set; } = string.Empty;
    public string  Subject         { get; set; } = string.Empty;
    public string  BodyPreview     { get; set; } = string.Empty;
    public DateTime LastActivityAt { get; set; }
    public DateTime? ReadAt        { get; set; }
    public int AttachmentCount     { get; set; }
    public int TaskCount           { get; set; }
}

// ── Ulæste pagineret ─────────────────────────────────────────────
public class UnreadMessagesViewModel : PagedViewModelBase
{
    public List<ThreadRowViewModel> Messages { get; set; } = new();
}

// ── Læste pagineret ──────────────────────────────────────────────
public class ReadMessagesViewModel : PagedViewModelBase
{
    public List<ThreadRowViewModel> Messages { get; set; } = new();
}

// ── Opgaver pagineret ────────────────────────────────────────────
public class TaskRowViewModel
{
    public int    Id              { get; set; }
    public int?   MessageId       { get; set; }
    public string Title           { get; set; } = string.Empty;
    public string? Description    { get; set; }
    public MessageTaskStatus Status { get; set; }
    public DateTime CreatedAt     { get; set; }
    public DateTime? DueDate      { get; set; }
    public string VolunteerName   { get; set; } = string.Empty;
    public string VolunteerKey    { get; set; } = string.Empty;
    public string MessageSubject  { get; set; } = string.Empty;
}

public class TasksViewModel : PagedViewModelBase
{
    public List<TaskRowViewModel> Tasks { get; set; } = new();
}

// ── Opret besked ─────────────────────────────────────────────────
public class CreateMessageViewModel
{
    public ThreadChannel Channel { get; set; } = ThreadChannel.App;
    public int    VolunteerId  { get; set; }
    public string Subject      { get; set; } = string.Empty;
    public string Body         { get; set; } = string.Empty;

    // Til dropdown i formularen
    public List<Volunteer> AvailableVolunteers { get; set; } = new();
}

// ── Vis besked detalje ───────────────────────────────────────────
public class MessageDetailViewModel
{
    public Message Message      { get; set; } = null!;
    public string SentByName    { get; set; } = string.Empty;
    public List<ReplyRowViewModel> Replies { get; set; } = new();
}

// ── Svar i tråd ──────────────────────────────────────────────────
public class ReplyRowViewModel
{
    public int    Id            { get; set; }
    public string Body          { get; set; } = string.Empty;
    public DateTime SentAt      { get; set; }
    public MessageDirection Direction { get; set; }
    public string SenderName    { get; set; } = string.Empty;
    public double? Latitude     { get; set; }
    public double? Longitude    { get; set; }
}

// ── Besvar besked ────────────────────────────────────────────────
public class ReplyMessageViewModel
{
    public int    MessageId { get; set; }
    public string Body      { get; set; } = string.Empty;
}

// ── Vis sms-tråd (én løbende samtale pr. frivillig/nummer) ───────
public class SmsThreadDetailViewModel
{
    public int?    VolunteerId   { get; set; }
    public string? PhoneNumber   { get; set; }
    public string  VolunteerName { get; set; } = string.Empty;
    public string  VolunteerKey  { get; set; } = string.Empty;
    public List<SmsThreadMessageViewModel> Messages { get; set; } = new();
}

public class SmsThreadMessageViewModel
{
    public int    Id          { get; set; }
    public SmsDirection Direction { get; set; }
    public string Body        { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? SentByName { get; set; }
}

// ── Opret opgave ─────────────────────────────────────────────────
public class CreateTaskViewModel
{
    public int    MessageId   { get; set; }
    public string Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DueDate    { get; set; }   // ISO date string fra input[type=date]
}

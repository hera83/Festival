using System;

namespace web.Models;

public class MessageAttachment
{
    public int Id { get; set; }

    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;

    // Original filnavn (hvad brugeren valgte)
    public string OriginalFileName { get; set; } = string.Empty;

    // Gemt filnavn under App_files/beskeder/
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;   // f.eks. image/jpeg, video/mp4
    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;
    public string UploadedByUserId { get; set; } = string.Empty;
}

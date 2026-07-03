namespace web.Services.Sms.Dtos.Sms;

// Payload gatewayen POST'er til vores webhook når der modtages en sms.
// Eksempel: { "index": 3, "status": "REC UNREAD", "originator": "+4512345678",
//             "timestamp": "2026-07-03T10:30:00+02:00", "body": "Hej verden" }
public sealed class SmsWebhookPayload
{
    public int? Index { get; init; }
    public string? Status { get; init; }
    public string? Originator { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string Body { get; init; } = string.Empty;
}

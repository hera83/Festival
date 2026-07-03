using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services.Sms.Dtos.Sms;
using web.Utils;

namespace web.Controllers;

// Offentligt, u-autoriseret endpoint — SMS-gatewayen leverer indgående sms'er
// hertil via det webhook der automatisk sættes på abonnementslister
// (se AdminController.GetSystemWebhookUrl). Ingen adgangskontrol her, jf.
// eksplicit valg om ikke at sikre endpointet særskilt.
[ApiController]
[Route("sms/webhook")]
public class SmsWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SmsWebhookController> _logger;

    public SmsWebhookController(ApplicationDbContext db, ILogger<SmsWebhookController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] SmsWebhookPayload payload, CancellationToken ct)
    {
        if (payload is null || string.IsNullOrWhiteSpace(payload.Body))
            return BadRequest();

        var normalizedFrom = PhoneNumbers.NormalizeDanishOrNull(payload.Originator);
        var phoneForStorage = normalizedFrom ?? payload.Originator ?? string.Empty;
        var occurredAt = payload.Timestamp?.DateTime ?? AppTime.Now;
        var season = AppTime.CurrentSeason;

        // Undgå dubletter hvis gatewayen leverer det samme webhook-kald mere end én gang.
        var alreadyExists = await _db.SmsMessages.AnyAsync(m =>
            m.Direction == SmsDirection.Inbound &&
            m.PhoneNumberSnapshot == phoneForStorage &&
            m.MessageBody == payload.Body &&
            m.OccurredAt == occurredAt, ct);
        if (alreadyExists)
            return Ok();

        int? volunteerId = null;
        if (normalizedFrom is not null)
        {
            var volunteers = await _db.Volunteers
                .Where(v => v.SeasonId == season && v.PhoneNumber != null && v.PhoneNumber != "")
                .ToListAsync(ct);
            var matched = volunteers.FirstOrDefault(v => PhoneNumbers.NormalizeDanishOrNull(v.PhoneNumber) == normalizedFrom);
            volunteerId = matched?.Id;
        }

        // Rå modem-status ("REC UNREAD"/"REC READ") bruges kun til at udlede IsUnread —
        // den viste Status skal altid bare være "Modtaget" for indgående sms'er.
        var isUnread = (payload.Status ?? string.Empty).Contains("UNREAD", StringComparison.OrdinalIgnoreCase);

        _db.SmsMessages.Add(new SmsMessage
        {
            SeasonId = season,
            Direction = SmsDirection.Inbound,
            VolunteerId = volunteerId,
            PhoneNumberSnapshot = phoneForStorage,
            MessageBody = payload.Body,
            Status = "Modtaget",
            IsUnread = isUnread,
            OccurredAt = occurredAt,
            CreatedAt = AppTime.Now
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Modtog sms fra {Phone} (frivillig-match: {VolunteerId}).", phoneForStorage, volunteerId?.ToString() ?? "ingen");

        return Ok();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services.Email;
using web.Utils;
using System.IO;

namespace web.Controllers;

[AllowAnonymous]
public class AppFrivilligController(ApplicationDbContext db, IEmailService emailService, IWebHostEnvironment env) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Overblik()
    {
        return PartialView("_Overblik");
    }

    [HttpGet]
    public async Task<IActionResult> OverblikData(int volunteerId, int seasonId)
    {
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == volunteerId && v.SeasonId == seasonId);

        if (volunteer == null)
            return NotFound();

        // Hent alle vagter med ShiftType
        var shifts = await db.Shifts
            .Include(s => s.ShiftType)
            .Where(s => s.VolunteerId == volunteerId && s.SeasonId == seasonId)
            .ToListAsync();

        var today = AppTime.CopenhagenToday;
        var now = AppTime.CopenhagenNow;

        var shiftDtos = shifts.Select(s => new
        {
            s.Id,
            shiftTypeId = s.ShiftTypeId,
            name = s.ShiftType.ShiftName,
            startLocal = s.ShiftType.StartTime,
            endLocal = s.ShiftType.EndTime,
        }).OrderBy(s => s.startLocal).ToList();

        // Dagens check-in sessioner (inkl. afsluttede – for "ingen nag" reglen)
        var todayCheckIns = await db.VolunteerCheckIns
            .Where(c => c.VolunteerId == volunteerId && c.SeasonId == seasonId && c.CheckInDate == today)
            .OrderByDescending(c => c.CheckedInAt)
            .ToListAsync();

        // Aktiv session = ikke checket ud
        var activeCheckIn = todayCheckIns.FirstOrDefault(c => c.CheckedOutAt == null);

        // Lokationslog for aktiv session
        List<object> locationLogs = [];
        if (activeCheckIn != null)
        {
            var logs = await db.VolunteerLocationLogs
                .Where(l => l.CheckInId == activeCheckIn.Id)
                .OrderBy(l => l.OccurredAt)
                .ToListAsync();

            locationLogs = logs.Select(l => (object)new
            {
                l.EventType,
                l.Location,
                occurredAt = AppTime.ToCopenhagen(l.OccurredAt)
            }).ToList();
        }

        // Har frivillig haft en check-in i dag (selv afsluttet)?
        var hasHadCheckInToday = todayCheckIns.Any();

        return Ok(new
        {
            name = volunteer.Name,
            nowLocal = now,
            todayDate = today.ToString("yyyy-MM-dd"),
            shifts = shiftDtos,
            hasHadCheckInToday,
            activeCheckIn = activeCheckIn == null ? null : new
            {
                id = activeCheckIn.Id,
                checkedInAt = AppTime.ToCopenhagen(activeCheckIn.CheckedInAt),
                checkedOutAt = (DateTime?)null,
                currentLocation = activeCheckIn.CurrentLocation,
                locationLogs
            }
        });
    }

    [HttpGet]
    public IActionResult Vagter()
    {
        return PartialView("_Vagter");
    }

    [HttpGet]
    public async Task<IActionResult> VagterData(int volunteerId, int seasonId)
    {
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == volunteerId && v.SeasonId == seasonId);

        if (volunteer == null)
            return NotFound();

        var shifts = await db.Shifts
            .Include(s => s.ShiftType)
            .Where(s => s.VolunteerId == volunteerId && s.SeasonId == seasonId)
            .ToListAsync();

        if (!shifts.Any())
            return Ok(new { shifts = Array.Empty<object>() });

        var now = AppTime.CopenhagenNow;

        // Find alle dage hvor den frivillige har haft check-in denne sæson
        var checkInDates = await db.VolunteerCheckIns
            .Where(c => c.VolunteerId == volunteerId && c.SeasonId == seasonId)
            .Select(c => c.CheckInDate)
            .Distinct()
            .ToListAsync();

        var checkInDateSet = checkInDates.ToHashSet();

        var result = shifts
            .Select(s =>
            {
                var startLocal = s.ShiftType.StartTime;
                var endLocal = s.ShiftType.EndTime;
                var shiftDate = DateOnly.FromDateTime(startLocal);

                string status;
                var today = DateOnly.FromDateTime(now);
                if (endLocal > now && shiftDate == today)
                    status = "i dag";
                else if (endLocal > now)
                    status = "kommende";
                else if (checkInDateSet.Contains(shiftDate))
                    status = "afviklet";
                else
                    status = "udeblivelse";

                return new
                {
                    id = s.Id,
                    name = s.ShiftType.ShiftName,
                    startLocal,
                    endLocal,
                    date = shiftDate.ToString("yyyy-MM-dd"),
                    status
                };
            })
            .OrderByDescending(s => s.startLocal)
            .ToList();

        return Ok(new { shifts = result });
    }

    [HttpGet]
    public IActionResult Observationer()
    {
        return PartialView("_Observationer");
    }

    [HttpGet]
    public IActionResult Beskeder()
    {
        return PartialView("_Beskeder");
    }

    [HttpGet]
    public IActionResult Profil()
    {
        return PartialView("_Profil");
    }

    [HttpGet]
    public IActionResult Telefonbog()
    {
        return PartialView("_Telefonbog");
    }

    // ── App profil login ──────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> LookupEmail([FromBody] LookupEmailRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { error = "Ingen mail angivet." });

        var season = AppTime.CurrentSeason;
        var volunteers = await db.Volunteers
            .Where(v => v.SeasonId == season && v.Email != null && v.Email.ToLower() == req.Email.ToLower())
            .Select(v => new { v.Id, v.Name })
            .ToListAsync();

        if (!volunteers.Any())
            return Ok(new { found = false });

        return Ok(new { found = true, volunteers });
    }

    [HttpPost]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest req)
    {
        var season = AppTime.CurrentSeason;
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.SeasonId == season && v.Id == req.VolunteerId);

        if (volunteer == null)
            return BadRequest(new { error = "Frivillig ikke fundet." });

        var code = Random.Shared.Next(100000, 999999).ToString();
        var expiry = AppTime.Now.AddMinutes(15);

        var meta = await db.VolunteerMetas.FirstOrDefaultAsync(m => m.VolunteerId == volunteer.Id);
        if (meta == null)
        {
            meta = new VolunteerMeta { VolunteerId = volunteer.Id };
            db.VolunteerMetas.Add(meta);
        }
        meta.AppConfirmCode = code;
        meta.AppConfirmCodeExpiry = expiry;
        meta.UpdatedAt = AppTime.Now;
        await db.SaveChangesAsync();

        var html = $"""
            <div style="font-family:Inter,sans-serif;max-width:480px;margin:0 auto;background:#0d1117;color:#e6edf3;padding:32px;border-radius:12px;">
                <h2 style="color:#e85d2e;margin-top:0;">Festival Vagtstyring</h2>
                <p>Hej <strong>{volunteer.Name}</strong>,</p>
                <p>Din bekræftelseskode til appen er:</p>
                <div style="font-size:2.5rem;font-weight:700;letter-spacing:0.35em;text-align:center;background:#161b22;padding:20px;border-radius:8px;color:#e85d2e;margin:24px 0;">
                    {code}
                </div>
                <p>Koden er gyldig i 15 minutter. Indtast den i appen under Profil for at logge ind.</p>
                <p style="color:#8b949e;font-size:0.85rem;">Hvis du ikke har bedt om denne kode, kan du roligt ignorere denne mail.</p>
            </div>
            """;

        await emailService.SendEmailAsync(volunteer.Email!, "Din bekræftelseskode til appen", html, volunteer.Name);

        return Ok(new { sent = true });
    }

    [HttpPost]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest req)
    {
        var season = AppTime.CurrentSeason;
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.SeasonId == season && v.Id == req.VolunteerId);

        if (volunteer == null)
            return BadRequest(new { error = "Frivillig ikke fundet." });

        var meta = await db.VolunteerMetas.FirstOrDefaultAsync(m => m.VolunteerId == volunteer.Id);
        if (meta == null || meta.AppConfirmCode != req.Code || meta.AppConfirmCodeExpiry < AppTime.Now)
            return Ok(new { valid = false, error = "Forkert eller udløbet kode." });

        meta.AppConfirmCode = null;
        meta.AppConfirmCodeExpiry = null;
        // Første gang appen aktiveres – gem installationstidspunkt og enhed
        if (meta.AppInstalledAt == null)
            meta.AppInstalledAt = AppTime.Now;
        meta.AppDeviceName = ParseDeviceName(Request.Headers.UserAgent.ToString());
        meta.UpdatedAt = AppTime.Now;
        await db.SaveChangesAsync();

        return Ok(new
        {
            valid = true,
            volunteerId = volunteer.Id,
            seasonId = volunteer.SeasonId,
            name = volunteer.Name,
            key = volunteer.Key,
            email = volunteer.Email,
            phone = volunteer.PhoneNumber,
            qrToken = volunteer.QrToken
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile(int volunteerId, int seasonId)
    {
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == volunteerId && v.SeasonId == seasonId);

        if (volunteer == null)
            return NotFound();

        return Ok(new
        {
            volunteer.Id,
            volunteer.SeasonId,
            volunteer.Name,
            volunteer.Key,
            volunteer.Email,
            phone = volunteer.PhoneNumber,
            volunteer.QrToken
        });
    }

    [HttpPost]
    public async Task<IActionResult> SaveProfile([FromBody] SaveProfileRequest req)
    {
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == req.VolunteerId && v.SeasonId == req.SeasonId);

        if (volunteer == null)
            return BadRequest(new { error = "Frivillig ikke fundet." });

        volunteer.Email = req.Email?.Trim();
        volunteer.PhoneNumber = req.Phone?.Trim();
        volunteer.UpdatedAt = AppTime.Now;
        await db.SaveChangesAsync();

        return Ok(new { saved = true });
    }

    // ── Beskeder ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> BeskederData(int volunteerId, int seasonId)
    {
        var messages = await db.Messages
            .Where(m => m.VolunteerId == volunteerId && m.SeasonId == seasonId && !m.IsDeleted)
            .Include(m => m.Replies)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        var result = messages.Select(m =>
        {
            // Ulæst for frivillig = der er et Outbound svar/den originale besked er Outbound
            // som er nyere end hvornår frivillig sidst åbnede tråden
            var lastCoordActivity = m.Replies
                .Where(r => r.Direction == MessageDirection.Outbound)
                .OrderByDescending(r => r.SentAt)
                .Select(r => (DateTime?)r.SentAt)
                .FirstOrDefault();

            // Hvis beskeden selv er Outbound (sendt af koordinator) og aldrig åbnet
            if (lastCoordActivity == null && m.Direction == MessageDirection.Outbound)
                lastCoordActivity = m.SentAt;

            bool hasUnread = lastCoordActivity.HasValue &&
                (m.VolunteerOpenedAt == null || lastCoordActivity > m.VolunteerOpenedAt);

            return new
            {
                m.Id,
                m.Subject,
                m.Body,
                sentAt = AppTime.ToCopenhagen(m.SentAt).ToString("dd/MM/yyyy HH:mm"),
                isCoordinator = m.Direction == MessageDirection.Outbound,
                hasUnread,
                replyCount = m.Replies.Count,
                replies = m.Replies.OrderBy(r => r.SentAt).Select(r => new
                {
                    r.Id,
                    r.Body,
                    sentAt = AppTime.ToCopenhagen(r.SentAt).ToString("dd/MM HH:mm"),
                    isCoordinator = r.Direction == MessageDirection.Outbound
                })
            };
        });

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> MarkOpenedByVolunteer([FromBody] MarkOpenedRequest req)
    {
        var msg = await db.Messages
            .FirstOrDefaultAsync(m => m.Id == req.MessageId && m.VolunteerId == req.VolunteerId);
        if (msg == null) return Ok(); // fail silently

        msg.VolunteerOpenedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SendBesked([FromBody] SendBeskedRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Emne og besked er påkrævet." });

        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == req.VolunteerId && v.SeasonId == req.SeasonId);

        if (volunteer == null)
            return BadRequest(new { error = "Frivillig ikke fundet." });

        db.Messages.Add(new Message
        {
            SeasonId     = req.SeasonId,
            VolunteerId  = req.VolunteerId,
            SentByUserId = string.Empty,
            Direction    = MessageDirection.Inbound,
            Subject      = req.Subject.Trim(),
            Body         = req.Body.Trim(),
            IsRead       = false,
            SentAt       = DateTime.Now,
            Latitude     = req.Latitude,
            Longitude    = req.Longitude
        });

        await db.SaveChangesAsync();
        return Ok(new { sent = true });
    }

    [HttpPost]
    public async Task<IActionResult> BeskedReply([FromBody] BeskedReplyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Svar kan ikke være tomt." });

        var msg = await db.Messages
            .FirstOrDefaultAsync(m => m.Id == req.MessageId && m.VolunteerId == req.VolunteerId);

        if (msg == null)
            return BadRequest(new { error = "Besked ikke fundet." });

        db.MessageReplies.Add(new MessageReply
        {
            MessageId    = req.MessageId,
            SentByUserId = null,
            Direction    = MessageDirection.Inbound,
            Body         = req.Body.Trim(),
            SentAt       = DateTime.Now,
            Latitude     = req.Latitude,
            Longitude    = req.Longitude
        });

        // Koordinator skal se det som ulæst igen
        msg.IsRead = false;
        msg.ReadAt = null;

        await db.SaveChangesAsync();
        return Ok(new { sent = true });
    }

    [HttpPost]
    public async Task<IActionResult> SendObservation(
        [FromForm] int volunteerId, [FromForm] int seasonId,
        [FromForm] string message, [FromForm] string type, IFormFile? file,
        [FromForm] double? latitude, [FromForm] double? longitude)
    {
        if (string.IsNullOrWhiteSpace(message))
            return BadRequest(new { error = "Besked er påkrævet." });

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Fil er påkrævet." });

        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == volunteerId && v.SeasonId == seasonId);

        if (volunteer == null)
            return BadRequest(new { error = "Frivillig ikke fundet." });

        // Gem fil
        var dir = Path.Combine(env.ContentRootPath, "App_files", "beskeder");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(file.FileName);
        var storedName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(dir, storedName);
        await using (var fs = System.IO.File.Create(filePath))
            await file.CopyToAsync(fs);

        var subject = $"Observation – {(type == "video" ? "Video" : "Foto")} {AppTime.CopenhagenNow:dd/MM/yyyy HH:mm}";

        var msg = new Message
        {
            SeasonId     = seasonId,
            VolunteerId  = volunteerId,
            SentByUserId = string.Empty,
            Direction    = MessageDirection.Inbound,
            Subject      = subject,
            Body         = message.Trim(),
            IsRead       = false,
            SentAt       = DateTime.Now
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        db.MessageAttachments.Add(new MessageAttachment
        {
            MessageId        = msg.Id,
            OriginalFileName = file.FileName,
            StoredFileName   = storedName,
            ContentType      = file.ContentType,
            FileSizeBytes    = file.Length,
            UploadedAt       = DateTime.Now,
            UploadedByUserId = string.Empty,
            Latitude         = latitude,
            Longitude        = longitude
        });

        await db.SaveChangesAsync();
        return Ok(new { sent = true });
    }

    [HttpPost]
    public async Task<IActionResult> LogGpsLocation([FromBody] GpsLocationRequest req)
    {
        var volunteer = await db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == req.VolunteerId && v.SeasonId == req.SeasonId);

        if (volunteer == null)
            return Ok(); // fail silently – vi vil ikke forstyrre app-flowet

        // Kun gem GPS-data hvis den frivillige er aktiv checket ind
        var today = AppTime.CopenhagenToday;
        var isCheckedIn = await db.VolunteerCheckIns
            .AnyAsync(c => c.VolunteerId == volunteer.Id && c.SeasonId == volunteer.SeasonId
                        && c.CheckInDate == today && c.CheckedOutAt == null);

        if (!isCheckedIn)
            return Ok(new { logged = false, reason = "not_checked_in" });

        db.VolunteerGpsLogs.Add(new VolunteerGpsLog
        {
            VolunteerId   = volunteer.Id,
            SeasonId      = volunteer.SeasonId,
            VolunteerKey  = volunteer.Key,
            VolunteerName = volunteer.Name,
            Latitude      = req.Latitude,
            Longitude     = req.Longitude,
            Accuracy      = req.Accuracy,
            Trigger       = req.Trigger ?? "Unknown",
            LoggedAt      = AppTime.Now
        });

        // Slet GPS-logs der er ældre end 24 timer (løbende oprydning)
        var cutoff = AppTime.Now.AddHours(-24);
        await db.VolunteerGpsLogs
            .Where(l => l.LoggedAt < cutoff)
            .ExecuteDeleteAsync();

        await db.SaveChangesAsync();
        return Ok(new { logged = true });
    }
    private static string ParseDeviceName(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Ukendt enhed";

        // Samsung
        var m = System.Text.RegularExpressions.Regex.Match(userAgent, @"Samsung[- ]([A-Z0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) return $"Samsung {m.Groups[1].Value}";

        // Generisk Android-model: (Linux; Android X.X; ModelName)
        m = System.Text.RegularExpressions.Regex.Match(userAgent, @"Android [^;]+;\s*([^)]+)\)");
        if (m.Success)
        {
            var model = m.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(model)) return model;
        }

        // iPhone
        if (userAgent.Contains("iPhone")) return "iPhone";
        // iPad
        if (userAgent.Contains("iPad")) return "iPad";
        // Windows
        if (userAgent.Contains("Windows")) return "Windows";
        // Mac
        if (userAgent.Contains("Macintosh")) return "Mac";

        return "Ukendt enhed";
    }
}

public record LookupEmailRequest(string Email);
public record SendCodeRequest(int VolunteerId);
public record VerifyCodeRequest(int VolunteerId, string Code);
public record SaveProfileRequest(int VolunteerId, int SeasonId, string? Email, string? Phone);
public record SendBeskedRequest(int VolunteerId, int SeasonId, string Subject, string Body, double? Latitude, double? Longitude);
public record BeskedReplyRequest(int MessageId, int VolunteerId, string Body, double? Latitude, double? Longitude);
public record MarkOpenedRequest(int MessageId, int VolunteerId);
public record GpsLocationRequest(int VolunteerId, int SeasonId, double Latitude, double Longitude, double? Accuracy, string? Trigger);

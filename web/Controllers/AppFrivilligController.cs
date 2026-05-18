using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services.Email;
using web.Utils;

namespace web.Controllers;

[AllowAnonymous]
public class AppFrivilligController(ApplicationDbContext db, IEmailService emailService) : Controller
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
    public IActionResult Vagter()
    {
        return PartialView("_Vagter");
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
        var expiry = AppTime.UtcNow.AddMinutes(15);

        var meta = await db.VolunteerMetas.FirstOrDefaultAsync(m => m.VolunteerId == volunteer.Id);
        if (meta == null)
        {
            meta = new VolunteerMeta { VolunteerId = volunteer.Id };
            db.VolunteerMetas.Add(meta);
        }
        meta.AppConfirmCode = code;
        meta.AppConfirmCodeExpiry = expiry;
        meta.UpdatedAt = AppTime.UtcNow;
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
        if (meta == null || meta.AppConfirmCode != req.Code || meta.AppConfirmCodeExpiry < AppTime.UtcNow)
            return Ok(new { valid = false, error = "Forkert eller udløbet kode." });

        meta.AppConfirmCode = null;
        meta.AppConfirmCodeExpiry = null;
        meta.UpdatedAt = AppTime.UtcNow;
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
        volunteer.UpdatedAt = AppTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { saved = true });
    }
}

public record LookupEmailRequest(string Email);
public record SendCodeRequest(int VolunteerId);
public record VerifyCodeRequest(int VolunteerId, string Code);
public record SaveProfileRequest(int VolunteerId, int SeasonId, string? Email, string? Phone);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers;

[Authorize]
public class AppCheckInController : Controller
{
    private readonly ApplicationDbContext _db;

    public AppCheckInController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> LookupQr(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest();

        var seasonId = AppTime.CurrentSeason;

        var volunteer = await _db.Volunteers
            .FirstOrDefaultAsync(v => v.Key == token.Trim() && v.SeasonId == seasonId);

        if (volunteer == null)
            return NotFound();

        var today = AppTime.CopenhagenToday;
        var alreadyCheckedIn = await _db.VolunteerCheckIns
            .AnyAsync(c => c.SeasonId == seasonId && c.VolunteerId == volunteer.Id && c.CheckInDate == today && c.CheckedOutAt == null);

        return Json(new
        {
            volunteer.Id,
            volunteer.Name,
            volunteer.Key,
            AlreadyCheckedIn = alreadyCheckedIn
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCheckIn([FromBody] AppCheckInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new { result = "error", message = "Ugyldig QR kode." });

        var seasonId = AppTime.CurrentSeason;
        var today = AppTime.CopenhagenToday;

        var volunteer = await _db.Volunteers
            .FirstOrDefaultAsync(v => v.Key == request.Key.Trim() && v.SeasonId == seasonId);

        if (volunteer == null)
            return Json(new { result = "notfound", message = "Ingen frivillig fundet." });

        // Find en åben session (ikke udchecket) – præcis samme logik som Dashboard
        var existing = await _db.VolunteerCheckIns
            .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == volunteer.Id && c.CheckInDate == today && c.CheckedOutAt == null);

        if (existing != null)
            return Json(new { result = "duplicate", name = volunteer.Name, message = $"{volunteer.Name} er allerede checket ind." });

        var now = AppTime.Now;
        var checkIn = new VolunteerCheckIn
        {
            SeasonId = seasonId,
            VolunteerId = volunteer.Id,
            CheckInDate = today,
            CheckedInAt = now,
            CurrentLocation = "Pit"
        };
        _db.VolunteerCheckIns.Add(checkIn);

        // Et evt. aktivt snooze (udsat udeblivelsesvarsel) er ikke længere
        // relevant, nu hvor den frivillige rent faktisk er mødt op.
        var snooze = await _db.NoShowSnoozes
            .FirstOrDefaultAsync(sn => sn.SeasonId == seasonId && sn.VolunteerId == volunteer.Id);
        if (snooze != null)
            _db.NoShowSnoozes.Remove(snooze);

        await _db.SaveChangesAsync();

        _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
        {
            CheckInId = checkIn.Id,
            VolunteerId = volunteer.Id,
            SeasonId = seasonId,
            EventType = "CheckIn",
            Location = "Pit",
            OccurredAt = now
        });
        await _db.SaveChangesAsync();

        return Json(new { result = "ok", name = volunteer.Name, message = $"{volunteer.Name} er nu checket ind." });
    }
}

public class AppCheckInRequest
{
    public string Key { get; set; } = "";
}

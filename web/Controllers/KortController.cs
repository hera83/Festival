using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Utils;

namespace web.Controllers;

[Authorize]
public class KortController(ApplicationDbContext db) : Controller
{
    private static int CurrentSeason => AppTime.CurrentSeason;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GpsData()
    {
        var season = CurrentSeason;

        // Hent nyeste GPS-entry pr. frivillig med volunteer-info
        var maxPerVolunteer = db.VolunteerGpsLogs
            .Where(l => l.SeasonId == season)
            .GroupBy(l => l.VolunteerId)
            .Select(g => new { VolunteerId = g.Key, MaxLoggedAt = g.Max(l => l.LoggedAt) });

        var latest = await db.VolunteerGpsLogs
            .Where(l => l.SeasonId == season)
            .Join(maxPerVolunteer,
                l => new { l.VolunteerId, MaxLoggedAt = l.LoggedAt },
                m => new { m.VolunteerId, m.MaxLoggedAt },
                (l, _) => l)
            .Join(db.Volunteers,
                log => log.VolunteerId,
                v   => v.Id,
                (log, v) => new
                {
                    log.VolunteerId,
                    v.Name,
                    email       = v.Email ?? "",
                    phone       = v.PhoneNumber ?? "",
                    log.Latitude,
                    log.Longitude,
                    log.Accuracy,
                    loggedAt    = log.LoggedAt
                })
            .ToListAsync();

        var now = AppTime.Now;

        var result = latest.Select(l => new
        {
            l.VolunteerId,
            l.Name,
            email           = l.email,
            phone           = l.phone,
            l.Latitude,
            l.Longitude,
            l.Accuracy,
            loggedAt        = AppTime.ToCopenhagen(l.loggedAt).ToString("dd/MM/yyyy HH:mm:ss"),
            ageMinutes      = (now - l.loggedAt).TotalMinutes,
            isRecent        = (now - l.loggedAt).TotalHours < 1
        });

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> PoiData()
    {
        var season = CurrentSeason;

        var pois = await db.MapLocations
            .Where(p => p.SeasonId == season)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                p.Latitude,
                p.Longitude,
                p.Description
            })
            .ToListAsync();

        return Json(pois);
    }
}

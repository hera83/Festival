using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers;

[Authorize]
public class DashboardSettingController(ApplicationDbContext db) : Controller
{
    // GET /DashboardSetting/Get?key=PitAlarmMinutes
    [HttpGet]
    public async Task<IActionResult> Get(string key)
    {
        var seasonId = AppTime.CurrentSeason;
        var setting = await db.DashboardSettings
            .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == key);
        return Json(new { value = setting?.Value });
    }

    // POST /DashboardSetting/Set
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set([FromBody] SetSettingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Key))
            return Json(new { success = false, message = "Ugyldig nøgle." });

        var seasonId = AppTime.CurrentSeason;
        var setting = await db.DashboardSettings
            .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == req.Key);

        if (setting == null)
        {
            setting = new DashboardSetting { SeasonId = seasonId, Key = req.Key };
            db.DashboardSettings.Add(setting);
        }

        setting.Value = string.IsNullOrWhiteSpace(req.Value) ? null : req.Value;
        setting.UpdatedAt = AppTime.Now;
        await db.SaveChangesAsync();

        return Json(new { success = true });
    }

    public record SetSettingRequest(string Key, string? Value);
}

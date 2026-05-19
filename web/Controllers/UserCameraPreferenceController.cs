using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers;

[Authorize]
public class UserCameraPreferenceController(ApplicationDbContext db) : Controller
{
    // GET /UserCameraPreference/Get
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var pref = await db.UserCameraPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref == null)
            return Json(new { deviceId = (string?)null, deviceFingerprint = (string?)null });

        return Json(new { deviceId = pref.DeviceId, deviceFingerprint = pref.DeviceFingerprint });
    }

    // POST /UserCameraPreference/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveCameraRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.DeviceFingerprint))
            return Json(new { success = false, message = "Manglende data." });

        var pref = await db.UserCameraPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref == null)
        {
            pref = new UserCameraPreference { UserId = userId };
            db.UserCameraPreferences.Add(pref);
        }

        pref.DeviceId = req.DeviceId;
        pref.DeviceFingerprint = req.DeviceFingerprint;
        pref.UpdatedAt = AppTime.Now;
        await db.SaveChangesAsync();

        return Json(new { success = true });
    }

    // POST /UserCameraPreference/Clear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var pref = await db.UserCameraPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref != null)
        {
            db.UserCameraPreferences.Remove(pref);
            await db.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    public record SaveCameraRequest(string DeviceId, string DeviceFingerprint);
}

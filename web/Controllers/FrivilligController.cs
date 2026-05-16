using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;

namespace web.Controllers;

[Authorize]
public class FrivilligController(ApplicationDbContext db) : Controller
{
    private static int CurrentSeason => DateTime.Now.Year;

    public async Task<IActionResult> Index()
    {
        var season = CurrentSeason;
        var vm = new FrivilligIndexViewModel
        {
            VolunteerCount = await db.Volunteers.CountAsync(v => v.SeasonId == season),
            ShiftTypeCount = await db.ShiftTypes.CountAsync(st => st.SeasonId == season),
            ShiftCount     = await db.Shifts.CountAsync(s => s.SeasonId == season)
        };

        return View(vm);
    }

    // ── Overblik ──────────────────────────────────────────────────
    public IActionResult OverblikPartial() => PartialView("_OverblikPartial");

    public async Task<IActionResult> OverblikDage()
    {
        var shiftTypes = await db.ShiftTypes
            .Where(st => st.SeasonId == CurrentSeason)
            .Select(st => st.StartTime)
            .ToListAsync();

        var dates = shiftTypes
            .Select(dt => dt.Date)
            .Distinct()
            .OrderBy(d => d)
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList();

        return Json(dates);
    }

    public async Task<IActionResult> OverblikData(string date, int startHour = 0)
    {
        if (!DateOnly.TryParse(date, out var dateOnly))
            return BadRequest("Ugyldig dato");

        var windowStart = dateOnly.ToDateTime(new TimeOnly(startHour, 0));
        var windowEnd   = windowStart.AddHours(24);
        var now         = DateTime.Now;

        // ── Vagttyper + tilmeldte ────────────────────────────────
        var shiftTypes = await db.ShiftTypes
            .Where(st => st.SeasonId == CurrentSeason && st.StartTime < windowEnd && st.EndTime > windowStart)
            .Include(st => st.Shifts)
            .ToListAsync();

        // ── Check-ins der overlapper vinduet ────────────────────
        var checkIns = await db.VolunteerCheckIns
            .Where(ci => ci.SeasonId == CurrentSeason
                      && ci.CheckedInAt < windowEnd
                      && (ci.CheckedOutAt == null || ci.CheckedOutAt > windowStart))
            .ToListAsync();

        var staffed  = new int[24];
        var required = new int[24];
        var checkedIn = new int[24];
        var isPast    = new bool[24];

        for (int i = 0; i < 24; i++)
        {
            var slotStart = windowStart.AddHours(i);
            var slotEnd   = slotStart.AddHours(1);

            isPast[i] = slotEnd <= now;

            foreach (var st in shiftTypes)
            {
                if (st.StartTime < slotEnd && st.EndTime > slotStart)
                {
                    required[i] += st.RequiredCount;
                    staffed[i]  += st.Shifts.Count;
                }
            }

            // Tæl frivillige der var tjekket ind i dette slot
            foreach (var ci in checkIns)
            {
                var ciOut = ci.CheckedOutAt ?? now; // åben session tæller til nu
                if (ci.CheckedInAt < slotEnd && ciOut > slotStart)
                    checkedIn[i]++;
            }
        }

        var labels = Enumerable.Range(0, 24)
            .Select(i => windowStart.AddHours(i).ToString("HH:00"))
            .ToList();

        return Json(new { labels, staffed, required, checkedIn, isPast });
    }

    // ── Frivillige ────────────────────────────────────────────────
    public Task<IActionResult> VolunteersPartial() => VolunteersSearch();

    public async Task<IActionResult> VolunteersSearch(string q = "", int page = 1, int pageSize = 10)
    {
        var query = db.Volunteers.Where(v => v.SeasonId == CurrentSeason);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(v =>
                v.Name.ToLower().Contains(ql) ||
                v.Key.ToLower().Contains(ql) ||
                (v.Email != null && v.Email.ToLower().Contains(ql)) ||
                (v.PhoneNumber != null && v.PhoneNumber.ToLower().Contains(ql)));
        }

        query = query.OrderBy(v => v.Name);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var vm = new VolunteersPagedViewModel
        {
            Volunteers = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
            Query      = q,
        };

        return PartialView("_VolunteersPartial", vm);
    }

    // ── Vagttyper ─────────────────────────────────────────────────
    public Task<IActionResult> ShiftTypesPartial() => ShiftTypesSearch();

    public async Task<IActionResult> ShiftTypesSearch(string q = "", int page = 1, int pageSize = 10)
    {
        var query = db.ShiftTypes.Where(st => st.SeasonId == CurrentSeason);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(st => st.ShiftName.ToLower().Contains(ql));
        }

        query = query.OrderBy(st => st.StartTime);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var vm = new ShiftTypesPagedViewModel
        {
            ShiftTypes = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
            Query      = q,
        };

        return PartialView("_ShiftTypesPartial", vm);
    }

    // ── Vagter ────────────────────────────────────────────────────
    public Task<IActionResult> ShiftsPartial() => ShiftsSearch();

    public async Task<IActionResult> ShiftsSearch(string q = "", int page = 1, int pageSize = 10)
    {
        var query = db.Shifts
            .Include(s => s.Volunteer)
            .Include(s => s.ShiftType)
            .Where(s => s.SeasonId == CurrentSeason);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(s =>
                s.Volunteer.Name.ToLower().Contains(ql) ||
                s.ShiftType.ShiftName.ToLower().Contains(ql));
        }

        query = query.OrderBy(s => s.ShiftType.StartTime).ThenBy(s => s.Volunteer.Name);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var vm = new ShiftsPagedViewModel
        {
            Shifts     = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
            Query      = q,
        };

        return PartialView("_ShiftsPartial", vm);
    }

    // ── Opret frivillig ───────────────────────────────────────────
    public async Task<IActionResult> GetCounts()
    {
        var season = CurrentSeason;
        return Json(new
        {
            volunteerCount = await db.Volunteers.CountAsync(v => v.SeasonId == season),
            shiftTypeCount = await db.ShiftTypes.CountAsync(st => st.SeasonId == season),
            shiftCount     = await db.Shifts.CountAsync(s => s.SeasonId == season),
        });
    }

    public async Task<IActionResult> GetCreateForm()
    {
        var vm = new CreateVolunteerViewModel
        {
            Key                  = Guid.NewGuid().ToString("N")[..8].ToUpper(),
            AvailableShiftTypes  = await db.ShiftTypes
                                           .Where(st => st.SeasonId == CurrentSeason)
                                           .OrderBy(st => st.StartTime)
                                           .ToListAsync()
        };
        return PartialView("_CreateVolunteerModal", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVolunteer([FromForm] CreateVolunteerViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Name))
            return Json(new { success = false, message = "Navn er påkrævet." });

        if (string.IsNullOrWhiteSpace(vm.Key))
            vm.Key = Guid.NewGuid().ToString("N")[..8].ToUpper();

        var keyExists = await db.Volunteers
            .AnyAsync(v => v.SeasonId == CurrentSeason && v.Key == vm.Key);
        if (keyExists)
            return Json(new { success = false, message = $"Nøglen '{vm.Key}' er allerede i brug." });

        var volunteer = new Volunteer
        {
            SeasonId    = CurrentSeason,
            Key         = vm.Key.Trim().ToUpper(),
            Name        = vm.Name.Trim(),
            Email       = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim(),
        };

        db.Volunteers.Add(volunteer);
        await db.SaveChangesAsync();

        if (vm.ShiftTypeIds.Count > 0)
        {
            var validIds = await db.ShiftTypes
                .Where(st => st.SeasonId == CurrentSeason && vm.ShiftTypeIds.Contains(st.Id))
                .Select(st => st.Id)
                .ToListAsync();

            foreach (var stId in validIds)
            {
                db.Shifts.Add(new Shift
                {
                    SeasonId    = CurrentSeason,
                    VolunteerId = volunteer.Id,
                    ShiftTypeId = stId,
                });
            }
            await db.SaveChangesAsync();
        }

        return Json(new { success = true, message = $"Frivillig '{volunteer.Name}' oprettet." });
    }

    public async Task<IActionResult> GetEditForm(int id)
    {
        var volunteer = await db.Volunteers
            .Include(v => v.Shifts)
            .FirstOrDefaultAsync(v => v.Id == id && v.SeasonId == CurrentSeason);

        if (volunteer is null) return NotFound();

        var vm = new EditVolunteerViewModel
        {
            Id                  = volunteer.Id,
            Key                 = volunteer.Key,
            Name                = volunteer.Name,
            Email               = volunteer.Email,
            PhoneNumber         = volunteer.PhoneNumber,
            ShiftTypeIds        = volunteer.Shifts.Select(s => s.ShiftTypeId).ToList(),
            AvailableShiftTypes = await db.ShiftTypes
                                          .Where(st => st.SeasonId == CurrentSeason)
                                          .OrderBy(st => st.StartTime)
                                          .ToListAsync(),
        };
        return PartialView("_EditVolunteerModal", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVolunteer([FromForm] EditVolunteerViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Name))
            return Json(new { success = false, message = "Navn er påkrævet." });

        var volunteer = await db.Volunteers
            .Include(v => v.Shifts)
            .FirstOrDefaultAsync(v => v.Id == vm.Id && v.SeasonId == CurrentSeason);

        if (volunteer is null) return Json(new { success = false, message = "Frivillig ikke fundet." });

        var keyTaken = await db.Volunteers
            .AnyAsync(v => v.SeasonId == CurrentSeason && v.Key == vm.Key.Trim().ToUpper() && v.Id != vm.Id);
        if (keyTaken)
            return Json(new { success = false, message = $"Nøglen '{vm.Key}' er allerede i brug." });

        volunteer.Key         = vm.Key.Trim().ToUpper();
        volunteer.Name        = vm.Name.Trim();
        volunteer.Email       = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        volunteer.PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim();
        volunteer.UpdatedAt   = DateTime.Now;

        // Synkroniser vagter
        var existingIds = volunteer.Shifts.Select(s => s.ShiftTypeId).ToHashSet();
        var desiredIds  = vm.ShiftTypeIds.ToHashSet();

        var toRemove = volunteer.Shifts.Where(s => !desiredIds.Contains(s.ShiftTypeId)).ToList();
        db.Shifts.RemoveRange(toRemove);

        var validNew = await db.ShiftTypes
            .Where(st => st.SeasonId == CurrentSeason && desiredIds.Except(existingIds).Contains(st.Id))
            .Select(st => st.Id)
            .ToListAsync();

        foreach (var stId in validNew)
            db.Shifts.Add(new Shift { SeasonId = CurrentSeason, VolunteerId = volunteer.Id, ShiftTypeId = stId });

        await db.SaveChangesAsync();
        return Json(new { success = true, message = $"Frivillig '{volunteer.Name}' opdateret." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVolunteer(int id)
    {
        var volunteer = await db.Volunteers
            .Include(v => v.Shifts)
            .FirstOrDefaultAsync(v => v.Id == id && v.SeasonId == CurrentSeason);

        if (volunteer is null) return Json(new { success = false, message = "Frivillig ikke fundet." });

        db.Shifts.RemoveRange(volunteer.Shifts);
        db.Volunteers.Remove(volunteer);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = $"Frivillig '{volunteer.Name}' slettet." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShiftType(int id)
    {
        var shiftType = await db.ShiftTypes
            .Include(st => st.Shifts)
            .FirstOrDefaultAsync(st => st.Id == id && st.SeasonId == CurrentSeason);

        if (shiftType is null) return Json(new { success = false, message = "Vagttype ikke fundet." });

        db.Shifts.RemoveRange(shiftType.Shifts);
        db.ShiftTypes.Remove(shiftType);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = $"Vagttype '{shiftType.ShiftName}' og tilhørende vagter er slettet." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShift(int id)
    {
        var shift = await db.Shifts
            .Include(s => s.Volunteer)
            .Include(s => s.ShiftType)
            .FirstOrDefaultAsync(s => s.Id == id && s.SeasonId == CurrentSeason);

        if (shift is null) return Json(new { success = false, message = "Vagt ikke fundet." });

        db.Shifts.Remove(shift);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = $"Vagten '{shift.ShiftType.ShiftName}' er fjernet fra {shift.Volunteer.Name}." });
    }

    // ── Behov ─────────────────────────────────────────────────────
    public Task<IActionResult> BehovPartial() => BehovSearch();

    public async Task<IActionResult> BehovSearch(string q = "", int page = 1, int pageSize = 10)
    {
        var query = db.ShiftTypes.Where(st => st.SeasonId == CurrentSeason);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(st => st.ShiftName.ToLower().Contains(ql));
        }

        query = query.OrderBy(st => st.StartTime);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var shiftTypes = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = shiftTypes.Select(st => st.Id).ToList();

        var signedUp = await db.Shifts
            .Where(s => s.SeasonId == CurrentSeason && ids.Contains(s.ShiftTypeId))
            .GroupBy(s => s.ShiftTypeId)
            .Select(g => new { ShiftTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ShiftTypeId, x => x.Count);

        var totalMissing = await db.ShiftTypes
            .Where(st => st.SeasonId == CurrentSeason)
            .Select(st => new { st.RequiredCount, Signed = db.Shifts.Count(s => s.SeasonId == CurrentSeason && s.ShiftTypeId == st.Id) })
            .SumAsync(x => Math.Max(0, x.RequiredCount - x.Signed));

        var rows = shiftTypes.Select(st => new BehovRow
        {
            ShiftTypeId   = st.Id,
            ShiftName     = st.ShiftName,
            StartTime     = st.StartTime,
            EndTime       = st.EndTime,
            RequiredCount = st.RequiredCount,
            SignedUpCount = signedUp.GetValueOrDefault(st.Id, 0),
        }).ToList();

        var vm = new BehovPagedViewModel
        {
            Rows         = rows,
            Page         = page,
            PageSize     = pageSize,
            TotalCount   = totalCount,
            Query        = q,
            TotalMissing = totalMissing,
        };

        return PartialView("_BehovPartial", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBehov(int shiftTypeId, int requiredCount)
    {
        var shiftType = await db.ShiftTypes
            .FirstOrDefaultAsync(st => st.Id == shiftTypeId && st.SeasonId == CurrentSeason);

        if (shiftType is null) return Json(new { success = false, message = "Vagttype ikke fundet." });

        if (requiredCount < 0) return Json(new { success = false, message = "Behov kan ikke være negativt." });

        shiftType.RequiredCount = requiredCount;
        shiftType.UpdatedAt     = DateTime.Now;
        await db.SaveChangesAsync();

        return Json(new { success = true, message = $"Behov for '{shiftType.ShiftName}' sat til {requiredCount}." });
    }
}

using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services.Sms;
using web.Services.Sms.Dtos.Subscriptions;
using web.Utils;

namespace web.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private const string DefaultShiftName = "Diverse";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ISmsService _smsService;
    private readonly ISmsMessageLogService _smsMessageLogService;
    private readonly ISmsGatewayStatusCache _smsGatewayStatusCache;

    public AdminController(
        ApplicationDbContext db,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ISmsService smsService,
        ISmsMessageLogService smsMessageLogService,
        ISmsGatewayStatusCache smsGatewayStatusCache)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _smsService = smsService;
        _smsMessageLogService = smsMessageLogService;
        _smsGatewayStatusCache = smsGatewayStatusCache;
    }

    public IActionResult Index(string tab = "brugere")
    {
        ViewData["ActiveTab"] = tab;
        return View();
    }

    // ── QR Scanner partial ───────────────────────────────────────
    public IActionResult QrScannerPartial() => PartialView("_QrScannerPartial");

    [HttpGet]
    public async Task<IActionResult> LookupQrToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest();

        var seasonId = AppTime.CurrentSeason;

        // QR-koden indeholder frivilligens Key (ikke QrToken)
        var volunteer = await _db.Volunteers
            .Include(v => v.Shifts)
            .FirstOrDefaultAsync(v => v.Key == token.Trim() && v.SeasonId == seasonId);

        if (volunteer == null)
            return NotFound();

        return Json(new
        {
            volunteer.Id,
            volunteer.Name,
            volunteer.Email,
            volunteer.PhoneNumber,
            volunteer.Key,
            volunteer.SeasonId,
            volunteer.QrCodeSent,
            ShiftCount = volunteer.Shifts.Count
        });
    }

    // ── Import & Eksport partial ─────────────────────────────────
    public IActionResult ImportExportPartial() => PartialView("_ImportExportPartial");

    // ── Statistik partial ────────────────────────────────────────
    public Task<IActionResult> StatistikPartial() => StatistikSearch();

    public async Task<IActionResult> StatistikSearch(string q = "", int page = 1, int pageSize = 10)
    {
        var seasonId = AppTime.CurrentSeason;

        // Hent alle frivillige for sæsonen
        var volunteersQuery = _db.Volunteers
            .Where(v => v.SeasonId == seasonId);

        if (!string.IsNullOrWhiteSpace(q))
            volunteersQuery = volunteersQuery.Where(v =>
                v.Name.Contains(q) ||
                v.Key.Contains(q) ||
                (v.Email != null && v.Email.Contains(q)));

        // Hent check-ins og beregn timer in-memory (SQLite har ikke DateDiff)
        var checkIns = await _db.VolunteerCheckIns
            .Where(c => c.SeasonId == seasonId && c.CheckedOutAt != null)
            .Select(c => new { c.VolunteerId, c.CheckedInAt, c.CheckedOutAt })
            .ToListAsync();

        var checkInMap = checkIns
            .GroupBy(c => c.VolunteerId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    CheckInCount = g.Count(),
                    TotalMinutes = g.Sum(c => (c.CheckedOutAt!.Value - c.CheckedInAt).TotalMinutes),
                });

        var totalCount = await volunteersQuery.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var volunteers = await volunteersQuery.ToListAsync();

        var rows = volunteers.Select(v =>
        {
            checkInMap.TryGetValue(v.Id, out var ci);
            return new StatistikVolunteerRow
            {
                Id           = v.Id,
                Key          = v.Key,
                Name         = v.Name,
                Email        = v.Email,
                PhoneNumber  = v.PhoneNumber,
                CheckInCount = ci?.CheckInCount ?? 0,
                TotalHours   = ci != null ? Math.Round(ci.TotalMinutes / 60.0, 1) : 0,
            };
        })
        .OrderByDescending(r => r.TotalHours)
        .ThenBy(r => r.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();

        var vm = new StatistikViewModel
        {
            Rows       = rows,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
        };
        return PartialView("_StatistikPartial", vm);
    }

    // ── Statistik detaljer (JSON) ─────────────────────────────────
    public async Task<IActionResult> VolunteerCheckInDetail(int volunteerId)
    {
        var seasonId = AppTime.CurrentSeason;

        var volunteer = await _db.Volunteers
            .FirstOrDefaultAsync(v => v.Id == volunteerId && v.SeasonId == seasonId);

        if (volunteer is null)
            return NotFound();

        var checkIns = await _db.VolunteerCheckIns
            .Where(c => c.VolunteerId == volunteerId && c.SeasonId == seasonId)
            .OrderBy(c => c.CheckedInAt)
            .ToListAsync();

        var checkInIds = checkIns.Select(c => c.Id).ToList();
        var locationLogs = await _db.VolunteerLocationLogs
            .Where(l => checkInIds.Contains(l.CheckInId))
            .OrderBy(l => l.OccurredAt)
            .ToListAsync();
        var logsByCheckIn = locationLogs.ToLookup(l => l.CheckInId);

        var now = AppTime.Now;

        var sessions = checkIns.Select(c =>
        {
            var checkOut  = c.CheckedOutAt ?? now;
            var isOpen    = c.CheckedOutAt == null;
            var local     = c.CheckedInAt;
            var localOut  = checkOut;
            var spansMidnight = local.Date != localOut.Date;

            var midnightLocal = local.Date;
            var startMin  = (local - midnightLocal).TotalMinutes;
            var endMin    = Math.Min((localOut - midnightLocal).TotalMinutes, 1440);
            var widthMin  = Math.Max(endMin - startMin, 10);

            var locations = logsByCheckIn[c.Id]
                .OrderBy(l => l.OccurredAt)
                .GroupBy(l => new { l.EventType, l.Location })   // fjern eksakte dubletter
                .Select(g => g.First())
                .OrderBy(l => l.OccurredAt)
                .Aggregate(new List<VolunteerLocationLog>(), (acc, l) =>  // fjern konsekutive dubletter
                {
                    if (acc.Count == 0 || acc[^1].EventType != l.EventType || acc[^1].Location != l.Location)
                        acc.Add(l);
                    return acc;
                })
                .Select(l => new LocationEventDto
                {
                    Time      = l.OccurredAt.ToString("HH:mm"),
                    EventType = l.EventType,
                    Location  = l.Location,
                })
                .ToList();

            return new DaySessionDto
            {
                Date          = local.ToString("ddd. d. MMM", new System.Globalization.CultureInfo("da-DK")),
                EndDate       = spansMidnight ? localOut.ToString("ddd. d. MMM", new System.Globalization.CultureInfo("da-DK")) : null,
                CheckInTime   = local.ToString("HH:mm"),
                CheckOutTime  = isOpen ? null : localOut.ToString("HH:mm"),
                StartAt       = local.ToString("yyyy-MM-ddTHH:mm:ss"),
                EndAt         = localOut.ToString("yyyy-MM-ddTHH:mm:ss"),
                DurationHours = Math.Round((checkOut - c.CheckedInAt).TotalHours, 1),
                LeftPct       = Math.Round(startMin / 1440.0 * 100, 2),
                WidthPct      = Math.Round(widthMin  / 1440.0 * 100, 2),
                SpansMidnight = spansMidnight,
                IsOpen        = isOpen,
                Locations     = locations,
            };
        }).ToList();

        var totalHours = checkIns
            .Where(c => c.CheckedOutAt != null)
            .Sum(c => (c.CheckedOutAt!.Value - c.CheckedInAt).TotalHours);

        var dto = new VolunteerDetailDto
        {
            Id         = volunteer.Id,
            Key        = volunteer.Key,
            Name       = volunteer.Name,
            TotalHours = Math.Round(totalHours, 1),
            Sessions   = sessions,
        };

        return Json(dto);
    }

    public IActionResult DownloadVolunteerTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Frivillige");

        // ── Kolonneoverskrifter ──────────────────────────────────
        var headers = new[]
        {
            "Key *",
            "Navn *",
            "Email",
            "Telefon",
            "Start * (dd-MM-yyyy HH:mm)",
            "Slut * (dd-MM-yyyy HH:mm)",
            "VagtNavn"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#161b22");
            cell.Style.Font.FontColor = XLColor.White;
        }

        // ── Eksempel-rækker (grå, udfyldt med gyldige data) ─────
        var examples = new[]
        {
            new[] { "FRV-001", "Anders Jensen",    "anders@example.com", "12345678", "20-06-2025 08:00", "20-06-2025 16:00", "Indgang Vest"  },
            new[] { "FRV-002", "Mette Christensen", "mette@example.com",  "87654321", "21-06-2025 12:00", "21-06-2025 20:00", "Scene Nord"    },
        };

        for (var r = 0; r < examples.Length; r++)
        {
            for (var c = 0; c < examples[r].Length; c++)
            {
                var cell = worksheet.Cell(r + 2, c + 1);
                cell.Value = examples[r][c];
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1c2128");
                cell.Style.Font.FontColor = XLColor.FromHtml("#8b949e");
                cell.Style.Font.Italic = true;
            }
        }

        // ── Hjælperække (forklaring) ──────────────────────────────
        var helpRow = examples.Length + 2;
        var hints = new[]
        {
            "Unik nøgle til frivillig",
            "Fulde navn",
            "Gyldig e-mailadresse",
            "8-cifret dansk nummer",
            "Format: dd-MM-yyyy HH:mm",
            "Format: dd-MM-yyyy HH:mm",
            "Navn på vagtholdet (tom = Diverse)"
        };

        for (var i = 0; i < hints.Length; i++)
        {
            var cell = worksheet.Cell(helpRow, i + 1);
            cell.Value = "→ " + hints[i];
            cell.Style.Font.FontColor = XLColor.FromHtml("#e85d2e");
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontSize = 9;
        }

        worksheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        // Tilføj note om obligatoriske felter i celle I1
        var noteCell = worksheet.Cell(1, headers.Length + 2);
        noteCell.Value = "* = Påkrævet felt";
        noteCell.Style.Font.Bold = true;
        noteCell.Style.Font.FontColor = XLColor.FromHtml("#e85d2e");
        noteCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#161b22");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Frivillige-Template.xlsx");
    }

    public async Task<IActionResult> ExportVolunteers()
    {
        var seasonId = AppTime.CurrentSeason;

        var shifts = await _db.Shifts
            .Include(s => s.Volunteer)
            .Include(s => s.ShiftType)
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.Volunteer.Key)
            .ThenBy(s => s.ShiftType.StartTime)
            .ToListAsync();

        // Frivillige uden vagter inkluderes også
        var volunteersWithShifts = shifts.Select(s => s.Volunteer.Id).ToHashSet();
        var volunteersNoShifts = await _db.Volunteers
            .Where(v => v.SeasonId == seasonId && !volunteersWithShifts.Contains(v.Id))
            .OrderBy(v => v.Key)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Frivillige");

        var headers = new[]
        {
            "Key *",
            "Navn *",
            "Email",
            "Telefon",
            "Start * (dd-MM-yyyy HH:mm)",
            "Slut * (dd-MM-yyyy HH:mm)",
            "VagtNavn"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#161b22");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = 2;

        foreach (var s in shifts)
        {
            ws.Cell(row, 1).Value = s.Volunteer.Key;
            ws.Cell(row, 2).Value = s.Volunteer.Name;
            ws.Cell(row, 3).Value = s.Volunteer.Email ?? string.Empty;
            ws.Cell(row, 4).Value = s.Volunteer.PhoneNumber ?? string.Empty;
            ws.Cell(row, 5).Value = s.ShiftType.StartTime.ToString("dd-MM-yyyy HH:mm");
            ws.Cell(row, 6).Value = s.ShiftType.EndTime.ToString("dd-MM-yyyy HH:mm");
            ws.Cell(row, 7).Value = s.ShiftType.ShiftName == DefaultShiftName ? string.Empty : s.ShiftType.ShiftName;
            row++;
        }

        foreach (var v in volunteersNoShifts)
        {
            ws.Cell(row, 1).Value = v.Key;
            ws.Cell(row, 2).Value = v.Name;
            ws.Cell(row, 3).Value = v.Email ?? string.Empty;
            ws.Cell(row, 4).Value = v.PhoneNumber ?? string.Empty;
            row++;
        }

        ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var filename = $"Frivillige-Eksport-{seasonId}.xlsx";
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    public async Task<IActionResult> ExportStatistik()
    {
        var seasonId = AppTime.CurrentSeason;

        var volunteers = await _db.Volunteers
            .Where(v => v.SeasonId == seasonId)
            .OrderBy(v => v.Key)
            .ToListAsync();

        var checkIns = await _db.VolunteerCheckIns
            .Where(c => c.SeasonId == seasonId && c.CheckedOutAt != null)
            .Select(c => new { c.VolunteerId, c.CheckedInAt, c.CheckedOutAt })
            .ToListAsync();

        var checkInMap = checkIns
            .GroupBy(c => c.VolunteerId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    CheckInCount = g.Count(),
                    TotalMinutes = g.Sum(c => (c.CheckedOutAt!.Value - c.CheckedInAt).TotalMinutes),
                });

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Statistik");

        var headers = new[] { "Nøgle", "Navn", "E-mail", "Telefon", "Antal check-ins", "Total timer" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#161b22");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var dataRow = 2;
        foreach (var v in volunteers)
        {
            checkInMap.TryGetValue(v.Id, out var ci);
            ws.Cell(dataRow, 1).Value = v.Key;
            ws.Cell(dataRow, 2).Value = v.Name;
            ws.Cell(dataRow, 3).Value = v.Email ?? string.Empty;
            ws.Cell(dataRow, 4).Value = v.PhoneNumber ?? string.Empty;
            ws.Cell(dataRow, 5).Value = ci?.CheckInCount ?? 0;
            ws.Cell(dataRow, 6).Value = ci != null ? Math.Round(ci.TotalMinutes / 60.0, 1) : 0;
            dataRow++;
        }

        ws.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var filename = $"Statistik-Timer-{seasonId}.xlsx";
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDataset()
    {
        var seasonId = AppTime.CurrentSeason;

        await using var tx = await _db.Database.BeginTransactionAsync();

        await _db.VolunteerLocationLogs
            .Where(l => l.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.VolunteerCheckIns
            .Where(c => c.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.Shifts
            .Where(s => s.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.Messages
            .Where(m => m.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.VolunteerGpsLogs
            .Where(g => g.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.VolunteerMetas
            .Where(vm => _db.Volunteers
                .Where(v => v.SeasonId == seasonId)
                .Select(v => v.Id)
                .Contains(vm.VolunteerId))
            .ExecuteDeleteAsync();

        await _db.ShiftTypes
            .Where(st => st.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.Volunteers
            .Where(v => v.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.Posts
            .Where(p => p.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.DashboardSettings
            .Where(d => d.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await _db.MapLocations
            .Where(m => m.SeasonId == seasonId)
            .ExecuteDeleteAsync();

        await tx.CommitAsync();

        TempData["Success"] = $"Dataset for sæson {seasonId} er blevet slettet.";
        return RedirectToAction("Index", new { tab = "importeksport" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewVolunteerImport(IFormFile? file)
    {
        var (error, vm) = BuildVolunteerImportPreview(file);
        if (error != null)
            return BadRequest(error);

        return PartialView("_VolunteerImportPreviewModal", vm!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportVolunteerData(IFormFile? file, bool includeBehovInDataset = false)
    {
        var (error, vm) = BuildVolunteerImportPreview(file);
        if (error != null)
            return BadRequest(new { success = false, message = error });

        var validRows = vm!.Rows
            .Where(r => !GetEffectiveErrors(r, includeBehovInDataset).Any())
            .ToList();

        if (validRows.Count != vm.Rows.Count)
            return BadRequest(new { success = false, message = "Import kan ikke gennemføres, fordi filen indeholder fejl." });

        if (validRows.Count == 0)
            return BadRequest(new { success = false, message = "Der er ingen gyldige rækker at importere." });

        var now = AppTime.Now;
        var currentSeasonId = AppTime.CurrentSeason;
        var seasonIds = new List<int> { currentSeasonId };
        var volunteerRows = validRows.Where(r => !r.IsBehovCandidate).ToList();
        var keys = volunteerRows
            .Select(r => r.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .ToList();

        await using var tx = await _db.Database.BeginTransactionAsync();

        var existingVolunteers = keys.Count == 0
            ? []
            : await _db.Volunteers
                .Where(v => seasonIds.Contains(v.SeasonId) && keys.Contains(v.Key))
                .ToListAsync();

        var volunteerMap = existingVolunteers.ToDictionary(v => (v.SeasonId, v.Key), v => v);
        var existingVolunteerIdsToReset = new HashSet<int>();

        foreach (var group in volunteerRows.GroupBy(r => (SeasonId: currentSeasonId, r.Key)))
        {
            var source = group.First();
            if (volunteerMap.TryGetValue(group.Key, out var existing))
            {
                existing.Name = source.Name;
                existing.Email = source.Email;
                existing.PhoneNumber = source.PhoneNumber;
                existing.UpdatedAt = now;
                existingVolunteerIdsToReset.Add(existing.Id);
                continue;
            }

            var newVolunteer = new Volunteer
            {
                SeasonId = group.Key.SeasonId,
                Key = group.Key.Key,
                Name = source.Name,
                Email = source.Email,
                PhoneNumber = source.PhoneNumber,
                QrToken = Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Volunteers.Add(newVolunteer);
            volunteerMap[group.Key] = newVolunteer;
        }

        await _db.SaveChangesAsync();

        if (existingVolunteerIdsToReset.Count > 0)
        {
            var shiftsToDelete = await _db.Shifts
                .Where(s => existingVolunteerIdsToReset.Contains(s.VolunteerId))
                .ToListAsync();

            if (shiftsToDelete.Count > 0)
                _db.Shifts.RemoveRange(shiftsToDelete);
        }

        var shiftTypeKeys = validRows
            .Select(r => new ShiftTypeImportKey(
                currentSeasonId,
                string.IsNullOrWhiteSpace(r.ShiftName) ? DefaultShiftName : r.ShiftName.Trim(),
                r.Start!.Value,
                r.End!.Value))
            .Distinct()
            .ToList();

        var existingShiftTypes = await _db.ShiftTypes
            .Where(st => seasonIds.Contains(st.SeasonId))
            .ToListAsync();

        var shiftTypeMap = existingShiftTypes.ToDictionary(
            st => new ShiftTypeImportKey(st.SeasonId, st.ShiftName, st.StartTime, st.EndTime),
            st => st);

        foreach (var key in shiftTypeKeys)
        {
            if (shiftTypeMap.TryGetValue(key, out var existingShiftType))
            {
                existingShiftType.UpdatedAt = now;
                continue;
            }

            var shiftType = new ShiftType
            {
                SeasonId = key.SeasonId,
                ShiftName = key.ShiftName,
                StartTime = key.Start,
                EndTime = key.End,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.ShiftTypes.Add(shiftType);
            shiftTypeMap[key] = shiftType;
        }

        if (includeBehovInDataset)
        {
            var requiredCountByShift = validRows
                .GroupBy(r => new ShiftTypeImportKey(
                    currentSeasonId,
                    string.IsNullOrWhiteSpace(r.ShiftName) ? DefaultShiftName : r.ShiftName.Trim(),
                    r.Start!.Value,
                    r.End!.Value))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in requiredCountByShift)
            {
                if (shiftTypeMap.TryGetValue(kvp.Key, out var shiftType))
                    shiftType.RequiredCount = kvp.Value;
            }
        }

        await _db.SaveChangesAsync();

        var shiftsToInsert = new List<Shift>(volunteerRows.Count);
        foreach (var row in volunteerRows)
        {
            var seasonId = currentSeasonId;
            var shiftName = string.IsNullOrWhiteSpace(row.ShiftName) ? DefaultShiftName : row.ShiftName.Trim();

            var volunteer = volunteerMap[(seasonId, row.Key)];
            var shiftType = shiftTypeMap[new ShiftTypeImportKey(seasonId, shiftName, row.Start!.Value, row.End!.Value)];

            shiftsToInsert.Add(new Shift
            {
                SeasonId = seasonId,
                VolunteerId = volunteer.Id,
                ShiftTypeId = shiftType.Id
            });
        }

        _db.Shifts.AddRange(shiftsToInsert);
        await _db.SaveChangesAsync();

        var unusedShiftTypes = await _db.ShiftTypes
            .Where(st => seasonIds.Contains(st.SeasonId)
                         && st.RequiredCount == 0
                         && !_db.Shifts.Any(s => s.ShiftTypeId == st.Id))
            .ToListAsync();

        if (unusedShiftTypes.Count > 0)
        {
            _db.ShiftTypes.RemoveRange(unusedShiftTypes);
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        return Json(new
        {
            success = true,
            message = $"Import gennemført: {volunteerRows.Count} tilmeldinger, {volunteerRows.Select(r => r.Key).Distinct().Count()} frivillige.",
            importedShiftCount = volunteerRows.Count
        });
    }

    private static (string? Error, VolunteerImportPreviewViewModel? Preview) BuildVolunteerImportPreview(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return ("Vælg en gyldig .xlsx-fil før import-preview.", null);

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null || worksheet.RangeUsed() == null)
            return ("Excel-filen indeholder ingen data.", null);

        var templateHeaders = new[]
        {
            "Key *",
            "Navn *",
            "Email",
            "Telefon",
            "Start * (dd-MM-yyyy HH:mm)",
            "Slut * (dd-MM-yyyy HH:mm)",
            "VagtNavn"
        };

        var dynamicHeaders = new[]
        {
            "Date",
            "Time",
            "Workplan",
            "Name",
            "Phone"
        };

        var headerMap = BuildHeaderMap(worksheet);
        var hasTemplateHeaders = templateHeaders.All(h => headerMap.ContainsKey(NormalizeHeader(h)));
        var hasDynamicHeaders = dynamicHeaders.All(h => headerMap.ContainsKey(NormalizeHeader(h)));

        if (!hasTemplateHeaders && !hasDynamicHeaders)
            return ("Filen matcher hverken standard-template eller den dynamiske import-struktur.", null);

        var rows = new List<VolunteerImportPreviewRowViewModel>();
        var usedRange = worksheet.RangeUsed()!;
        var firstDataRow = usedRange.FirstRowUsed().RowNumber() + 1;
        var lastRow = usedRange.LastRowUsed().RowNumber();
        var generatedKeyByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var rowNumber = firstDataRow; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            string key;
            string name;
            string email;
            string phone;
            DateTime? start;
            DateTime? end;
            string shiftNameRaw;

            if (hasTemplateHeaders)
            {
                key = GetCellByHeader(row, headerMap, "Key *").GetString().Trim();
                name = GetCellByHeader(row, headerMap, "Navn *").GetString().Trim();
                email = GetCellByHeader(row, headerMap, "Email").GetString().Trim();
                phone = GetCellByHeader(row, headerMap, "Telefon").GetString().Trim();
                start = ReadDateTime(GetCellByHeader(row, headerMap, "Start * (dd-MM-yyyy HH:mm)"));
                end = ReadDateTime(GetCellByHeader(row, headerMap, "Slut * (dd-MM-yyyy HH:mm)"));
                shiftNameRaw = GetCellByHeader(row, headerMap, "VagtNavn").GetString().Trim();
            }
            else
            {
                name = GetCellByHeader(row, headerMap, "Name").GetString().Trim();
                key = ResolveDynamicKey(name, generatedKeyByName);
                email = string.Empty;
                phone = GetCellByHeader(row, headerMap, "Phone").GetString().Trim();
                shiftNameRaw = GetCellByHeader(row, headerMap, "Workplan").GetString().Trim();

                var dateValue = ReadDateOnly(GetCellByHeader(row, headerMap, "Date"));
                var timeRange = GetCellByHeader(row, headerMap, "Time").GetString().Trim();
                (start, end) = ReadDynamicStartEnd(dateValue, timeRange);
            }

            var isEmptyRow = string.IsNullOrWhiteSpace(key)
                             && string.IsNullOrWhiteSpace(name)
                             && string.IsNullOrWhiteSpace(email)
                             && string.IsNullOrWhiteSpace(phone)
                             && !start.HasValue
                             && !end.HasValue
                             && string.IsNullOrWhiteSpace(shiftNameRaw);

            if (isEmptyRow)
                continue;

            var previewRow = new VolunteerImportPreviewRowViewModel
            {
                RowNumber = rowNumber - 1,
                Key = key,
                Name = name,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Start = start,
                End = end,
                ShiftName = string.IsNullOrWhiteSpace(shiftNameRaw) ? DefaultShiftName : shiftNameRaw,
                IsBehovCandidate = string.IsNullOrWhiteSpace(name)
            };

            if (string.IsNullOrWhiteSpace(previewRow.Key)) previewRow.Errors.Add("Key er påkrævet.");
            if (string.IsNullOrWhiteSpace(previewRow.Name)) previewRow.Errors.Add("Navn er påkrævet.");
            if (!previewRow.Start.HasValue) previewRow.Errors.Add("Start er påkrævet og skal kunne parses til DateTime.");
            if (!previewRow.End.HasValue) previewRow.Errors.Add("Slut er påkrævet og skal kunne parses til DateTime.");
            if (previewRow.Start.HasValue && previewRow.End.HasValue && previewRow.End.Value <= previewRow.Start.Value)
                previewRow.Errors.Add("Slut skal være senere end start.");

            if (!string.IsNullOrWhiteSpace(previewRow.Email) && !IsValidEmail(previewRow.Email))
                previewRow.Errors.Add("Email er ugyldig.");

            if (!string.IsNullOrWhiteSpace(previewRow.PhoneNumber) && !IsValidDanishPhone(previewRow.PhoneNumber))
                previewRow.Errors.Add("Telefonnummer er ikke et gyldigt dansk nummer.");

            rows.Add(previewRow);
        }

        return (null, new VolunteerImportPreviewViewModel
        {
            FileName = file.FileName,
            Rows = rows
        });
    }

    private static IReadOnlyList<string> GetEffectiveErrors(VolunteerImportPreviewRowViewModel row, bool includeBehovInDataset)
    {
        if (!includeBehovInDataset || !row.IsBehovCandidate)
            return row.Errors;

        return row.Errors
            .Where(error => !string.Equals(error, "Key er påkrævet.", StringComparison.Ordinal)
                            && !string.Equals(error, "Navn er påkrævet.", StringComparison.Ordinal)
                            && !string.Equals(error, "Email er ugyldig.", StringComparison.Ordinal)
                            && !string.Equals(error, "Telefonnummer er ikke et gyldigt dansk nummer.", StringComparison.Ordinal))
            .ToList();
    }

    private readonly record struct ShiftTypeImportKey(int SeasonId, string ShiftName, DateTime Start, DateTime End);

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
    {
        var map = new Dictionary<string, int>();
        var headerRow = worksheet.RangeUsed()?.FirstRowUsed();
        if (headerRow == null)
            return map;

        foreach (var cell in headerRow.CellsUsed())
        {
            var normalized = NormalizeHeader(cell.GetString());
            if (string.IsNullOrWhiteSpace(normalized) || map.ContainsKey(normalized))
                continue;

            map[normalized] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static string NormalizeHeader(string value)
    {
        return Regex.Replace(value ?? string.Empty, "\\s+", " ").Trim().ToLowerInvariant();
    }

    private static IXLCell GetCellByHeader(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string header)
    {
        if (!headerMap.TryGetValue(NormalizeHeader(header), out var columnNumber))
            return row.Cell(1);

        return row.Cell(columnNumber);
    }

    private static DateTime? ReadDateOnly(IXLCell cell)
    {
        var dateTime = ReadDateTime(cell);
        return dateTime?.Date;
    }

    private static (DateTime? Start, DateTime? End) ReadDynamicStartEnd(DateTime? date, string timeRange)
    {
        if (!date.HasValue || string.IsNullOrWhiteSpace(timeRange))
            return (null, null);

        var matches = Regex.Matches(timeRange, "(?<!\\d)(\\d{1,2})[:.](\\d{2})(?!\\d)");
        if (matches.Count < 2)
            return (null, null);

        if (!TryParseTime(matches[0].Value, out var startTime) || !TryParseTime(matches[1].Value, out var endTime))
            return (null, null);

        var start = date.Value.Date.Add(startTime);
        var end = date.Value.Date.Add(endTime);

        if (end <= start)
            end = end.AddDays(1);

        return (start, end);
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        var normalized = value.Replace('.', ':');
        return TimeSpan.TryParseExact(normalized, @"h\:mm", CultureInfo.InvariantCulture, out time)
               || TimeSpan.TryParseExact(normalized, @"hh\:mm", CultureInfo.InvariantCulture, out time);
    }

    private static string ResolveDynamicKey(string name, IDictionary<string, string> generatedKeyByName)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var clean = name.Trim();

        if (generatedKeyByName.TryGetValue(clean, out var existing))
            return existing;

        var key = $"Key-{generatedKeyByName.Count + 1:0000}";

        generatedKeyByName[clean] = key;
        return key;
    }

    private static DateTime? ReadDateTime(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.TryGetValue<DateTime>(out var dateTime))
            return dateTime;

        var text = cell.GetString().Trim();
        return DateTime.TryParse(text, out dateTime) ? dateTime : null;
    }

    private static bool IsValidEmail(string value)
    {
        if (value.Any(char.IsWhiteSpace))
            return false;

        // Reject non-ASCII characters (e.g. ø, æ, å) which are not valid in standard email addresses
        if (value.Any(c => c > 127))
            return false;

        try
        {
            var parsed = new System.Net.Mail.MailAddress(value);
            return string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidDanishPhone(string value) => PhoneNumbers.TryNormalizeDanish(value, out _);

    // ── Brugere partial ──────────────────────────────────────────
    public Task<IActionResult> UsersPartial() => UsersSearch();

    public async Task<IActionResult> UsersSearch(string q = "", int page = 1, int pageSize = 10)
    {
        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u =>
                (u.DisplayName != null && u.DisplayName.Contains(q)) ||
                (u.Email != null && u.Email.Contains(q)) ||
                (u.UserName != null && u.UserName.Contains(q)));

        query = query.OrderBy(u => u.DisplayName);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var pagedUsers = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        var rows = new List<AdminUserRow>();
        foreach (var u in pagedUsers)
        {
            rows.Add(new AdminUserRow
            {
                Id          = u.Id,
                DisplayName = u.DisplayName,
                Email       = u.Email ?? string.Empty,
                UserName    = u.UserName ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                Roles       = await _userManager.GetRolesAsync(u),
                IsLockedOut = await _userManager.IsLockedOutAsync(u),
                LastLogin   = u.LastLogin,
            });
        }

        var vm = new AdminUsersViewModel
        {
            Users         = rows,
            AllRoles      = allRoles,
            CurrentUserId = _userManager.GetUserId(User) ?? string.Empty,
            Page          = page,
            PageSize      = pageSize,
            TotalCount    = totalCount,
        };
        return PartialView("_UsersPartial", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index), new { tab = "brugere" });

        var user = new AppUser
        {
            DisplayName = model.DisplayName,
            UserName    = model.UserName,
            Email       = model.Email,
            PhoneNumber = model.PhoneNumber,
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.SelectedRole))
                await _userManager.AddToRoleAsync(user, model.SelectedRole);
            TempData["Success"] = $"Brugeren '{model.DisplayName}' blev oprettet.";
        }
        else
        {
            TempData["Error"] = "Opret bruger fejlede: " + string.Join(" ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Index), new { tab = "brugere" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string userId, string role, bool assign)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (assign)
            await _userManager.AddToRoleAsync(user, role);
        else
            await _userManager.RemoveFromRoleAsync(user, role);

        return RedirectToAction(nameof(Index), new { tab = "brugere" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                TempData["Success"] = $"Brugeren '{user.DisplayName}' blev slettet.";
            else
                TempData["Error"] = "Slet bruger fejlede: " + string.Join(" ", result.Errors.Select(e => e.Description));
        }
        else
        {
            TempData["Error"] = "Brugeren blev ikke fundet.";
        }

        return RedirectToAction(nameof(Index), new { tab = "brugere" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        user.DisplayName = model.DisplayName;
        user.Email = model.Email;
        user.PhoneNumber = model.PhoneNumber;

        // Brugernavn – brug SetUserNameAsync så Identity validerer for dubletter
        if (!string.Equals(user.UserName, model.UserName, StringComparison.OrdinalIgnoreCase))
        {
            var setUserName = await _userManager.SetUserNameAsync(user, model.UserName);
            if (!setUserName.Succeeded)
            {
                TempData["Error"] = "Gem bruger fejlede: " + string.Join(" ", setUserName.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index), new { tab = "brugere" });
            }
        }

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            TempData["Error"] = "Gem bruger fejlede: " + string.Join(" ", update.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index), new { tab = "brugere" });
        }

        // Opdater roller
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (model.SelectedRoles?.Count > 0)
            await _userManager.AddToRolesAsync(user, model.SelectedRoles);

        // Opdater låsestatus
        if (model.IsLockedOut)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else
            await _userManager.SetLockoutEndDateAsync(user, null);

        // Opdater password – kun hvis der er indtastet noget
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var removePassword = await _userManager.RemovePasswordAsync(user);
            if (!removePassword.Succeeded)
            {
                TempData["Error"] = "Gem bruger fejlede: " + string.Join(" ", removePassword.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index), new { tab = "brugere" });
            }

            var addPassword = await _userManager.AddPasswordAsync(user, model.NewPassword);
            if (!addPassword.Succeeded)
            {
                TempData["Error"] = "Gem bruger fejlede: " + string.Join(" ", addPassword.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index), new { tab = "brugere" });
            }
        }

        TempData["Success"] = $"Brugeren '{model.DisplayName}' blev opdateret.";
        return RedirectToAction(nameof(Index), new { tab = "brugere" });
    }

    // ── Kortsteder (POI) ─────────────────────────────────────────

    public async Task<IActionResult> KortStederPartial()
        => PartialView("_KortStederPartial", await KortStederQuery("", 1, 10));

    [HttpGet]
    public async Task<IActionResult> KortStederSearch(string q = "", int page = 1, int pageSize = 10)
        => PartialView("_KortStederPartial", await KortStederQuery(q, page, pageSize));

    private async Task<KortStederViewModel> KortStederQuery(string q, int page, int pageSize)
    {
        var season = AppTime.CurrentSeason;
        var query = _db.MapLocations
            .Where(p => p.SeasonId == season);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || p.Category.Contains(q) || (p.Description != null && p.Description.Contains(q)));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var allLocations = await _db.MapLocations
            .Where(p => p.SeasonId == season)
            .ToListAsync();

        return new KortStederViewModel
        {
            Items        = items,
            AllLocations = allLocations,
            Q            = q,
            Page         = page,
            PageSize     = pageSize,
            TotalCount   = total,
            TotalPages   = (int)Math.Ceiling(total / (double)pageSize),
            RangeFrom    = total == 0 ? 0 : (page - 1) * pageSize + 1,
            RangeTo      = Math.Min(page * pageSize, total)
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KortStederOpret(string name, string category, string latitude, string longitude, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Navn er påkrævet.";
            return RedirectToAction(nameof(Index), new { tab = "kortsteder" });
        }

        if (!double.TryParse(latitude,  System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(longitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            TempData["Error"] = "Ugyldige koordinater. Klik på kortet for at sætte positionen.";
            return RedirectToAction(nameof(Index), new { tab = "kortsteder" });
        }

        _db.MapLocations.Add(new web.Models.MapLocation
        {
            SeasonId    = AppTime.CurrentSeason,
            Name        = name.Trim(),
            Category    = category,
            Latitude    = lat,
            Longitude   = lng,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt   = AppTime.Now
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Stedet '{name.Trim()}' blev oprettet.";
        return RedirectToAction(nameof(Index), new { tab = "kortsteder" });
    }

    [HttpGet]
    public async Task<IActionResult> KortStederRediger(int id)
    {
        var poi = await _db.MapLocations.FindAsync(id);
        if (poi == null || poi.SeasonId != AppTime.CurrentSeason)
            return NotFound();
        return Json(new { poi.Id, poi.Name, poi.Category, poi.Latitude, poi.Longitude, poi.Description });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KortStederOpdater(int id, string name, string category, string latitude, string longitude, string? description)
    {
        var poi = await _db.MapLocations.FindAsync(id);
        if (poi == null || poi.SeasonId != AppTime.CurrentSeason)
            return NotFound();

        if (!double.TryParse(latitude,  System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(longitude, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            TempData["Error"] = "Ugyldige koordinater. Klik på kortet for at sætte positionen.";
            return RedirectToAction(nameof(Index), new { tab = "kortsteder" });
        }

        poi.Name        = name.Trim();
        poi.Category    = category;
        poi.Latitude    = lat;
        poi.Longitude   = lng;
        poi.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Stedet '{poi.Name}' blev opdateret.";
        return RedirectToAction(nameof(Index), new { tab = "kortsteder" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KortStederSlet(int id)
    {
        var poi = await _db.MapLocations.FindAsync(id);
        if (poi != null && poi.SeasonId == AppTime.CurrentSeason)
        {
            _db.MapLocations.Remove(poi);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Stedet '{poi.Name}' blev slettet.";
        }
        return RedirectToAction(nameof(Index), new { tab = "kortsteder" });
    }

    // ── SMS ───────────────────────────────────────────────────────

    public async Task<IActionResult> SmsPartial()
    {
        var vm = new SmsPartialViewModel
        {
            Status = await FetchAndCacheGatewayStatusAsync(),
            Log = await SmsLogQuery("", 1, 10)
        };

        var season = AppTime.CurrentSeason;
        vm.AllVolunteers = await _db.Volunteers
            .Where(v => v.SeasonId == season && v.PhoneNumber != null && v.PhoneNumber != "")
            .OrderBy(v => v.Name)
            .Select(v => new SmsVolunteerPickerItem { VolunteerId = v.Id, Name = v.Name, PhoneNumber = v.PhoneNumber! })
            .ToListAsync();

        return PartialView("_SmsPartial", vm);
    }

    // Henter frisk status/saldo hos gatewayen, opdaterer den delte cache (som
    // baggrundstjenesten SmsStatusUpdateService ellers holder ved lige hvert
    // 15. sekund) og returnerer den til den aktuelle sidevisning.
    private async Task<SmsGatewayStatusViewModel> FetchAndCacheGatewayStatusAsync()
    {
        var status = new SmsGatewayStatusViewModel();

        try
        {
            var health = await _smsService.GetHealthAsync();
            status.GatewayOnline = health is not null;
            status.HealthStatus = health?.Status;
            status.HealthTimestamp = health?.Timestamp;
        }
        catch (HttpRequestException ex)
        {
            status.GatewayOnline = false;
            status.GatewayErrorMessage = ex.Message;
        }

        try
        {
            var balance = await _smsService.GetBalanceCostAsync();
            status.Balance = balance?.Balance;
            status.BalanceUpdatedAt = balance?.UpdatedAt;
        }
        catch (HttpRequestException ex)
        {
            status.GatewayErrorMessage ??= ex.Message;
        }

        _smsGatewayStatusCache.Update(new SmsGatewayStatusSnapshot
        {
            Online = status.GatewayOnline,
            HealthStatus = status.HealthStatus,
            HealthTimestamp = status.HealthTimestamp,
            ErrorMessage = status.GatewayErrorMessage,
            Balance = status.Balance,
            BalanceUpdatedAt = status.BalanceUpdatedAt
        });

        return status;
    }

    // GET: /Admin/SmsGatewayStatus — læses fra den delte cache (ingen live gateway-kald),
    // så klientens 1-sekunds polling kan genopfriske saldo/status-kortene billigt.
    [HttpGet]
    public IActionResult SmsGatewayStatus()
    {
        var snapshot = _smsGatewayStatusCache.Current;
        var vm = new SmsGatewayStatusViewModel
        {
            GatewayOnline = snapshot.Online,
            HealthStatus = snapshot.HealthStatus,
            HealthTimestamp = snapshot.HealthTimestamp,
            GatewayErrorMessage = snapshot.ErrorMessage,
            Balance = snapshot.Balance,
            BalanceUpdatedAt = snapshot.BalanceUpdatedAt
        };
        return PartialView("_SmsStatusPartial", vm);
    }

    [HttpGet]
    public async Task<IActionResult> SmsLogSearch(string q = "", int page = 1, int pageSize = 10)
        => PartialView("_SmsLogTablePartial", await SmsLogQuery(q, page, pageSize));

    // GET: /Admin/SmsLogStateHash — let fingerprint af sms-loggen OG gateway-status/saldo,
    // bruges af klientens baggrunds-poller til at opdage ændringer (nye sms'er, statusskift,
    // saldo-ændring) uden at hente alle data eller kalde gatewayen live for hvert poll.
    [HttpGet]
    public async Task<IActionResult> SmsLogStateHash()
    {
        var season = AppTime.CurrentSeason;
        var rows = await _db.SmsMessages
            .Where(m => m.SeasonId == season)
            .Select(m => new { m.Id, m.Status })
            .ToListAsync();

        var snapshot = _smsGatewayStatusCache.Current;
        var raw = string.Join("|", rows.Select(r => $"{r.Id}:{r.Status}"))
            + $"||{snapshot.Online}|{snapshot.Balance}|{snapshot.HealthStatus}";
        var hash = raw.GetHashCode().ToString("X8");
        return Json(new { hash });
    }

    private async Task<SmsLogViewModel> SmsLogQuery(string q, int page, int pageSize)
    {
        var season = AppTime.CurrentSeason;
        var query =
            from m in _db.SmsMessages
            where m.SeasonId == season
            join v in _db.Volunteers on m.VolunteerId equals v.Id into vg
            from v in vg.DefaultIfEmpty()
            select new { m, v };

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x =>
                (x.v != null && x.v.Name.Contains(q)) ||
                x.m.PhoneNumberSnapshot.Contains(q) ||
                x.m.MessageBody.Contains(q));

        var total = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var rows = await query
            .OrderByDescending(x => x.m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var senderIds = rows.Where(x => x.m.SentByUserId != null).Select(x => x.m.SentByUserId!).Distinct().ToList();
        var senders = await _db.Users.Where(u => senderIds.Contains(u.Id)).ToListAsync();

        var items = rows.Select(x =>
        {
            var sender = x.m.SentByUserId is null ? null : senders.FirstOrDefault(u => u.Id == x.m.SentByUserId);
            return new SmsMessageRowViewModel
            {
                Id = x.m.Id,
                Direction = x.m.Direction,
                MessageId = x.m.MessageId,
                VolunteerId = x.v?.Id,
                VolunteerName = x.v?.Name,
                PhoneNumberSnapshot = x.m.PhoneNumberSnapshot,
                MessageBody = x.m.MessageBody,
                Status = x.m.Status,
                SegmentCount = x.m.SegmentCount,
                TotalPriceDkk = x.m.TotalPriceDkk,
                CreatedAt = x.m.CreatedAt,
                SentByDisplayName = x.m.Direction == SmsDirection.Inbound
                    ? null
                    : (sender?.DisplayName ?? sender?.UserName ?? "Ukendt")
            };
        }).ToList();

        return new SmsLogViewModel
        {
            Items = items,
            Q = q,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            RangeFrom = total == 0 ? 0 : (page - 1) * pageSize + 1,
            RangeTo = Math.Min(page * pageSize, total)
        };
    }

    public async Task<IActionResult> SmsAbonnementerListe()
    {
        var vm = new SmsSubscriptionListViewModel();
        var season = AppTime.CurrentSeason;
        var allVolunteers = await _db.Volunteers
            .Where(v => v.SeasonId == season && v.PhoneNumber != null && v.PhoneNumber != "")
            .OrderBy(v => v.Name)
            .Select(v => new SmsVolunteerPickerItem { VolunteerId = v.Id, Name = v.Name, PhoneNumber = v.PhoneNumber! })
            .ToListAsync();

        try
        {
            var subs = await _smsService.GetAllSubscriptionsAsync();
            var today = DateOnly.FromDateTime(AppTime.Now);

            vm.Items = subs.Select(s =>
            {
                var normalizedSubNumbers = s.PhoneNumbers
                    .Select(PhoneNumbers.NormalizeDanishOrNull)
                    .Where(n => n is not null)
                    .ToHashSet();
                var matched = allVolunteers
                    .Where(av => normalizedSubNumbers.Contains(PhoneNumbers.NormalizeDanishOrNull(av.PhoneNumber)))
                    .ToList();
                var matchedNormalizedNumbers = matched.Select(m => PhoneNumbers.NormalizeDanishOrNull(m.PhoneNumber)).ToHashSet();
                return new SmsSubscriptionRowViewModel
                {
                    Id = s.Id,
                    PhoneNumbers = s.PhoneNumbers,
                    MatchedVolunteers = matched,
                    UnmatchedPhoneNumbers = s.PhoneNumbers.Where(p => !matchedNormalizedNumbers.Contains(PhoneNumbers.NormalizeDanishOrNull(p))).ToList(),
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    WebhookUrl = s.WebhookUrl,
                    IsActive = s.IsActive,
                    IsCurrentlyInWindow = s.IsActive && s.StartDate <= today && s.EndDate >= today
                };
            })
            .OrderByDescending(x => x.IsCurrentlyInWindow)
            .ThenByDescending(x => x.StartDate)
            .ToList();
        }
        catch (HttpRequestException ex)
        {
            vm.Error = ex.Message;
        }

        return PartialView("_SmsAbonnementerListePartial", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SmsAbonnementOpret(List<int>? volunteerIds, DateOnly startDate, DateOnly endDate)
    {
        var (phoneNumbers, skippedCount) = await ResolveVolunteerPhoneNumbersAsync(volunteerIds);
        if (phoneNumbers.Count == 0)
        {
            TempData["Error"] = "Vælg mindst én frivillig med et gyldigt dansk telefonnummer.";
            return RedirectToAction(nameof(Index), new { tab = "sms" });
        }

        try
        {
            await _smsService.CreateSubscriptionAsync(new CreateSubscriptionsRequestDto
            {
                PhoneNumbers = phoneNumbers,
                StartDate = startDate,
                EndDate = endDate,
                WebhookUrl = GetSystemWebhookUrl()
            });
            TempData["Success"] = skippedCount > 0
                ? $"Abonnementslisten blev oprettet. {skippedCount} frivillig(e) blev udeladt pga. ugyldigt telefonnummer."
                : "Abonnementslisten blev oprettet.";
        }
        catch (HttpRequestException ex)
        {
            TempData["Error"] = "Kunne ikke oprette abonnementsliste: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "sms" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SmsAbonnementOpdater(Guid id, List<int>? volunteerIds, DateOnly startDate, DateOnly endDate)
    {
        var (phoneNumbers, skippedCount) = await ResolveVolunteerPhoneNumbersAsync(volunteerIds);
        if (phoneNumbers.Count == 0)
        {
            TempData["Error"] = "Vælg mindst én frivillig med et gyldigt dansk telefonnummer.";
            return RedirectToAction(nameof(Index), new { tab = "sms" });
        }

        try
        {
            await _smsService.UpdateSubscriptionAsync(id, new UpdateSubscriptionsRequestDto
            {
                PhoneNumbers = phoneNumbers,
                StartDate = startDate,
                EndDate = endDate,
                WebhookUrl = GetSystemWebhookUrl()
            });
            TempData["Success"] = skippedCount > 0
                ? $"Abonnementslisten blev opdateret. {skippedCount} frivillig(e) blev udeladt pga. ugyldigt telefonnummer."
                : "Abonnementslisten blev opdateret.";
        }
        catch (HttpRequestException ex)
        {
            TempData["Error"] = "Kunne ikke opdatere abonnementsliste: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "sms" });
    }

    // Webhook-URL sættes altid til vores eget faste endpoint — ikke noget admin vælger.
    private string GetSystemWebhookUrl() => $"{Request.Scheme}://{Request.Host}/sms/webhook";

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SmsAbonnementSlet(Guid id)
    {
        try
        {
            await _smsService.DeleteSubscriptionAsync(id);
            TempData["Success"] = "Abonnementslisten blev slettet.";
        }
        catch (HttpRequestException ex)
        {
            TempData["Error"] = "Kunne ikke slette abonnementsliste: " + ex.Message;
        }

        return RedirectToAction(nameof(Index), new { tab = "sms" });
    }

    // Returnerer kun numre der er gyldige danske 8-cifrede numre (SMS-gatewayen
    // kan intet andet) — resten tælles som "udeladt" så den kaldende action kan
    // fortælle admin at nogle frivillige ikke kunne med på listen.
    private async Task<(List<string> Numbers, int SkippedCount)> ResolveVolunteerPhoneNumbersAsync(List<int>? volunteerIds)
    {
        if (volunteerIds is null || volunteerIds.Count == 0) return ([], 0);
        var season = AppTime.CurrentSeason;
        var rawNumbers = await _db.Volunteers
            .Where(v => volunteerIds.Contains(v.Id) && v.SeasonId == season && v.PhoneNumber != null && v.PhoneNumber != "")
            .Select(v => v.PhoneNumber!)
            .ToListAsync();

        var valid = new List<string>();
        var skippedCount = 0;
        foreach (var raw in rawNumbers)
        {
            if (PhoneNumbers.TryNormalizeDanish(raw, out var normalized))
                valid.Add(normalized);
            else
                skippedCount++;
        }

        return (valid.Distinct().ToList(), skippedCount);
    }

    public async Task<IActionResult> SmsAfsendModtagere()
    {
        var vm = new SmsAfsendModtagereViewModel();
        try
        {
            vm.Items = await GetEligibleSmsVolunteersAsync();
        }
        catch (HttpRequestException ex)
        {
            vm.Error = ex.Message;
        }
        return PartialView("_SmsAfsendModtagerePartial", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SmsAfsend(List<int>? volunteerIds, string message)
    {
        if (volunteerIds is null || volunteerIds.Count == 0 || string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Vælg mindst én modtager og skriv en besked.";
            return RedirectToAction(nameof(Index), new { tab = "sms" });
        }

        List<SmsVolunteerPickerItem> eligible;
        try
        {
            eligible = await GetEligibleSmsVolunteersAsync();
        }
        catch (HttpRequestException ex)
        {
            TempData["Error"] = "Kunne ikke sende sms'er: " + ex.Message;
            return RedirectToAction(nameof(Index), new { tab = "sms" });
        }

        var eligibleIds = eligible.Select(e => e.VolunteerId).ToHashSet();
        var requestedIds = volunteerIds.Distinct().ToList();
        var toSend = requestedIds.Where(eligibleIds.Contains).ToList();
        var excludedCount = requestedIds.Count - toSend.Count;

        var senderId = _userManager.GetUserId(User) ?? string.Empty;
        var trimmedMessage = message.Trim();
        var results = new List<SmsSendResult>();
        foreach (var id in toSend)
        {
            results.Add(await _smsMessageLogService.SendAndLogAsync(id, trimmedMessage, senderId));
        }

        var successCount = results.Count(r => r.Success);
        var failed = results.Where(r => !r.Success).ToList();

        if (toSend.Count == 0)
        {
            TempData["Error"] = "Ingen af de valgte modtagere er længere på en aktiv abonnementsliste.";
            return RedirectToAction(nameof(Index), new { tab = "sms" });
        }

        var summary = $"{successCount} af {toSend.Count} sms'er sendt.";
        if (failed.Count > 0)
            summary += " Fejlede: " + string.Join(", ", failed.Select(f => $"{(string.IsNullOrEmpty(f.VolunteerName) ? "Ukendt" : f.VolunteerName)} ({f.ErrorMessage})"));
        if (excludedCount > 0)
            summary += $" {excludedCount} modtager(e) udeladt (ikke længere på en aktiv abonnementsliste).";

        if (successCount == toSend.Count && excludedCount == 0)
            TempData["Success"] = summary;
        else if (successCount == 0)
            TempData["Error"] = summary;
        else
            TempData["Warning"] = summary;

        return RedirectToAction(nameof(Index), new { tab = "sms" });
    }

    // Frivillige hvis telefonnummer er på en aktiv abonnementsliste (i dag inden for start-/slutdato).
    private async Task<List<SmsVolunteerPickerItem>> GetEligibleSmsVolunteersAsync()
    {
        var season = AppTime.CurrentSeason;
        var eligibleIds = await _smsMessageLogService.GetEligibleVolunteerIdsAsync(season);

        var volunteers = await _db.Volunteers
            .Where(v => v.SeasonId == season && eligibleIds.Contains(v.Id))
            .OrderBy(v => v.Name)
            .ToListAsync();

        return volunteers
            .Select(v => new SmsVolunteerPickerItem { VolunteerId = v.Id, Name = v.Name, PhoneNumber = v.PhoneNumber! })
            .ToList();
    }

    // ── System Logs ──────────────────────────────────────────────
    public IActionResult SystemLogsPartial() => PartialView("_SystemLogsPartial", new SystemLogsViewModel { PageSize = 10 });

    [HttpGet]
    public async Task<IActionResult> SystemLogsSearch(
        string q          = "",
        string level      = "",
        string dateFrom   = "",
        string dateTo     = "",
        bool   onlyErrors = false,
        int    page       = 1,
        int    pageSize   = 10)
    {
        if (pageSize < 1) pageSize = 25;
        if (page < 1)     page     = 1;

        var logDbPath = Path.Combine(
            HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().ContentRootPath,
            "App_dbs", "festival_logs.db");

        var whereClauses = new List<string>();
        var parameters   = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            whereClauses.Add("(RenderedMessage LIKE @q OR Exception LIKE @q OR Properties LIKE @q)");
            parameters.Add(new SqliteParameter("@q", $"%{q.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            whereClauses.Add("Level = @level");
            parameters.Add(new SqliteParameter("@level", level));
        }

        if (onlyErrors)
        {
            whereClauses.Add("(Level = 'Error' OR Level = 'Fatal' OR Exception IS NOT NULL AND Exception != '')");
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) &&
            DateTime.TryParse(dateFrom, out var df))
        {
            whereClauses.Add("Timestamp >= @dateFrom");
            parameters.Add(new SqliteParameter("@dateFrom", df.ToString("yyyy-MM-dd")));
        }

        if (!string.IsNullOrWhiteSpace(dateTo) &&
            DateTime.TryParse(dateTo, out var dt))
        {
            whereClauses.Add("Timestamp < @dateTo");
            parameters.Add(new SqliteParameter("@dateTo", dt.AddDays(1).ToString("yyyy-MM-dd")));
        }

        var whereStr = whereClauses.Count > 0
            ? "WHERE " + string.Join(" AND ", whereClauses)
            : "";

        int totalCount = 0;
        var rows = new List<SystemLogEntry>();

        await using var conn = new SqliteConnection($"Data Source={logDbPath}");
        await conn.OpenAsync();

        // Count
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM Logs {whereStr}";
            foreach (var p in parameters) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
            totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;
        int offset = (page - 1) * pageSize;

        // Rows
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT Id, Timestamp, Level, RenderedMessage, Exception, Properties FROM Logs {whereStr} ORDER BY Id DESC LIMIT @limit OFFSET @offset";
            foreach (var p in parameters) cmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
            cmd.Parameters.Add(new SqliteParameter("@limit",  pageSize));
            cmd.Parameters.Add(new SqliteParameter("@offset", offset));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new SystemLogEntry
                {
                    Id              = reader.GetInt64(0),
                    Timestamp       = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Level           = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    RenderedMessage = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Exception       = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Properties      = reader.IsDBNull(5) ? null : reader.GetString(5),
                });
            }
        }

        var vm = new SystemLogsViewModel
        {
            Rows       = rows,
            Q          = q,
            Level      = level,
            DateFrom   = dateFrom,
            DateTo     = dateTo,
            OnlyErrors = onlyErrors,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
        };

        return PartialView("_SystemLogsSearch", vm);
    }
}

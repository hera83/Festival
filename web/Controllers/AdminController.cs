using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private const string DefaultShiftName = "Diverse";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(ApplicationDbContext db, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
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

        var locationLogs = await _db.VolunteerLocationLogs.Where(l => l.SeasonId == seasonId).ToListAsync();
        _db.VolunteerLocationLogs.RemoveRange(locationLogs);

        var checkIns = await _db.VolunteerCheckIns.Where(c => c.SeasonId == seasonId).ToListAsync();
        _db.VolunteerCheckIns.RemoveRange(checkIns);

        var shifts = await _db.Shifts.Where(s => s.SeasonId == seasonId).ToListAsync();
        _db.Shifts.RemoveRange(shifts);

        var shiftTypes = await _db.ShiftTypes.Where(st => st.SeasonId == seasonId).ToListAsync();
        _db.ShiftTypes.RemoveRange(shiftTypes);

        var volunteers = await _db.Volunteers.Where(v => v.SeasonId == seasonId).ToListAsync();
        _db.Volunteers.RemoveRange(volunteers);

        await _db.SaveChangesAsync();

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
    public async Task<IActionResult> ImportVolunteerData(IFormFile? file)
    {
        var (error, vm) = BuildVolunteerImportPreview(file);
        if (error != null)
            return BadRequest(new { success = false, message = error });

        if (vm!.ErrorCount > 0)
            return BadRequest(new { success = false, message = "Import kan ikke gennemføres, fordi filen indeholder fejl." });

        var validRows = vm.Rows.Where(r => !r.HasErrors).ToList();
        if (validRows.Count == 0)
            return BadRequest(new { success = false, message = "Der er ingen gyldige rækker at importere." });

        var now = AppTime.Now;
        var currentSeasonId = AppTime.CurrentSeason;
        var seasonIds = new List<int> { currentSeasonId };
        var keys = validRows.Select(r => r.Key).Distinct().ToList();

        await using var tx = await _db.Database.BeginTransactionAsync();

        var existingVolunteers = await _db.Volunteers
            .Where(v => seasonIds.Contains(v.SeasonId) && keys.Contains(v.Key))
            .ToListAsync();

        var volunteerMap = existingVolunteers.ToDictionary(v => (v.SeasonId, v.Key), v => v);
        var existingVolunteerIdsToReset = new HashSet<int>();

        foreach (var group in validRows.GroupBy(r => (SeasonId: currentSeasonId, r.Key)))
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
                r.Start.Value,
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

        await _db.SaveChangesAsync();

        var shiftsToInsert = new List<Shift>(validRows.Count);
        foreach (var row in validRows)
        {
            var seasonId = currentSeasonId;
            var shiftName = string.IsNullOrWhiteSpace(row.ShiftName) ? DefaultShiftName : row.ShiftName.Trim();

            var volunteer = volunteerMap[(seasonId, row.Key)];
            var shiftType = shiftTypeMap[new ShiftTypeImportKey(seasonId, shiftName, row.Start.Value, row.End!.Value)];

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
            .Where(st => seasonIds.Contains(st.SeasonId) && !_db.Shifts.Any(s => s.ShiftTypeId == st.Id))
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
            message = $"Import gennemført: {validRows.Count} vagter, {validRows.Select(r => r.Key).Distinct().Count()} frivillige.",
            importedShiftCount = validRows.Count
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
                ShiftName = string.IsNullOrWhiteSpace(shiftNameRaw) ? DefaultShiftName : shiftNameRaw
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

    private static bool IsValidDanishPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("45") && digits.Length == 10)
            digits = digits[2..];

        return digits.Length == 8;
    }

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
}

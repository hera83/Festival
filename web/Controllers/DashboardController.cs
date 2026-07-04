using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using web.Data;
using web.Models;
using web.Services.Sms;
using web.Utils;

namespace web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IDashboardSmsFlowService _smsFlowService;
        private readonly ISmsGatewayStatusCache _smsGatewayStatusCache;

        public DashboardController(ApplicationDbContext db, IDashboardSmsFlowService smsFlowService, ISmsGatewayStatusCache smsGatewayStatusCache)
        {
            _db = db;
            _smsFlowService = smsFlowService;
            _smsGatewayStatusCache = smsGatewayStatusCache;
        }

        // Kaldes KUN fra de manuelle dashboard-handlinger nedenfor (aldrig fra
        // baggrundsjob eller fra det polling der holder boardet i sync på tværs
        // af åbne faner), så der uanset antallet af åbne faner kun sendes præcis
        // én sms pr. faktisk hændelse. Planlagte flytninger sender via samme
        // IDashboardSmsFlowService, men trigges fra ScheduledMoveService når de
        // rent faktisk udføres.
        private Task SendTemplatedSmsAsync(
            SmsTemplateType type, int volunteerId, string volunteerName, DateTime when,
            string? post = null, string? fraPost = null, string? tilPost = null)
        {
            var sentByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            return _smsFlowService.SendTemplatedSmsAsync(
                type, volunteerId, volunteerName, when, post, fraPost, tilPost, sentByUserId);
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET /Dashboard/GetSetting?key=PitAlarmMinutes
        [HttpGet]
        public async Task<IActionResult> GetSetting(string key)
        {
            var seasonId = AppTime.CurrentSeason;
            var setting = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == key);
            return Json(new { value = setting?.Value });
        }

        // POST /Dashboard/SetSetting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSetting([FromBody] SetSettingRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Key))
                return Json(new { success = false, message = "Ugyldig nøgle." });

            var seasonId = AppTime.CurrentSeason;
            var setting = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == req.Key);

            if (setting == null)
            {
                setting = new DashboardSetting { SeasonId = seasonId, Key = req.Key };
                _db.DashboardSettings.Add(setting);
            }

            setting.Value = string.IsNullOrWhiteSpace(req.Value) ? null : req.Value;
            setting.UpdatedAt = AppTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        // GET /Dashboard/GetSmsFlowStatus — bruges af sms-flow-knappen i Pitten og SMS-cardet
        [HttpGet]
        public async Task<IActionResult> GetSmsFlowStatus()
        {
            var seasonId = AppTime.CurrentSeason;
            var setting = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == SmsFlowSetting.Key);
            var autoOff = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == SmsFlowSetting.AutoOffAtKey);

            return Json(new { enabled = setting?.Value == "true", autoOffAt = autoOff?.Value });
        }

        // POST /Dashboard/SetSmsFlowEnabled — som SetSetting, men afviser tænd hvis
        // sms-saldoen er 0 eller ikke rækker til sms-taksten.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSmsFlowEnabled([FromBody] SetSmsFlowEnabledRequest req)
        {
            if (req.Enabled)
            {
                var status = _smsGatewayStatusCache.Current;
                var balance = status.Balance;
                var tooLow = !balance.HasValue || balance.Value <= 0m ||
                    (status.SmsPriceDkk.HasValue && balance.Value < status.SmsPriceDkk.Value);

                if (tooLow)
                    return Json(new { success = false, message = "SMS-flow kan ikke slås til – saldoen er for lav til at sende sms'er." });
            }

            var seasonId = AppTime.CurrentSeason;
            var setting = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == SmsFlowSetting.Key);

            if (setting == null)
            {
                setting = new DashboardSetting { SeasonId = seasonId, Key = SmsFlowSetting.Key };
                _db.DashboardSettings.Add(setting);
            }

            setting.Value = req.Enabled ? "true" : "false";
            setting.UpdatedAt = AppTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class SetSmsFlowEnabledRequest
        {
            public bool Enabled { get; set; }
        }

        // GET: /Dashboard/CheckInSearch?q=...&date=yyyy-MM-dd
        [HttpGet]
        public async Task<IActionResult> CheckInSearch(string? q, string? date)
        {
            var today = date != null && DateOnly.TryParse(date, out var d) ? d : AppTime.CopenhagenToday;
            var seasonId = today.Year;

            // Frivillige der allerede er checket ind (åben session)
            var alreadyCheckedInIds = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckedOutAt == null)
                .Select(c => c.VolunteerId)
                .ToListAsync();

            // Frivillige med vagt den pågældende dag
            var withShiftTodayIds = await _db.Shifts
                .Where(s => s.SeasonId == seasonId &&
                            s.ShiftType.StartTime.Year == today.Year &&
                            s.ShiftType.StartTime.Month == today.Month &&
                            s.ShiftType.StartTime.Day == today.Day)
                .Select(s => s.VolunteerId)
                .Distinct()
                .ToListAsync();

            IQueryable<Volunteer> query = _db.Volunteers.Where(v => v.SeasonId == seasonId);

            if (string.IsNullOrWhiteSpace(q))
            {
                // Vis kun frivillige med vagt i dag og som ikke er checket ind
                query = query.Where(v => withShiftTodayIds.Contains(v.Id) && !alreadyCheckedInIds.Contains(v.Id));
            }
            else
            {
                // Søg på alle – inkl. allerede indcheckede
                var term = q.Trim().ToLower();
                query = query.Where(v =>
                    v.Name.ToLower().Contains(term) || (v.Email != null && v.Email.ToLower().Contains(term)));
            }

            var volunteers = await query.ToListAsync();

            var result = volunteers
                .Select(v => new
                {
                    v.Id,
                    v.Name,
                    v.Email,
                    v.PhoneNumber,
                    HasShiftToday = withShiftTodayIds.Contains(v.Id),
                    IsCheckedIn = alreadyCheckedInIds.Contains(v.Id)
                })
                .OrderByDescending(v => v.HasShiftToday)
                .ThenBy(v => v.Name)
                .ToList();

            return Json(result);
        }

        // POST: /Dashboard/CheckIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
        {
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var volunteer = await _db.Volunteers.FindAsync(request.VolunteerId);
            if (volunteer == null)
                return Json(new { success = false, message = "Frivillig ikke fundet." });

            var checkIn = new VolunteerCheckIn
            {
                SeasonId = seasonId,
                VolunteerId = request.VolunteerId,
                CheckInDate = today,
                CheckedInAt = AppTime.Now,
                CurrentLocation = "Pit"
            };
            _db.VolunteerCheckIns.Add(checkIn);
            await _db.SaveChangesAsync();

            var log = new VolunteerLocationLog
            {
                CheckInId = checkIn.Id,
                VolunteerId = request.VolunteerId,
                SeasonId = seasonId,
                EventType = "CheckIn",
                Location = "Pit",
                OccurredAt = AppTime.Now
            };
            _db.VolunteerLocationLogs.Add(log);
            await _db.SaveChangesAsync();

            await SendTemplatedSmsAsync(SmsTemplateType.CheckIn, volunteer.Id, volunteer.Name, log.OccurredAt, post: "Pit");

            return Json(new { success = true, message = $"{volunteer.Name} er nu checket ind i Pitten." });
        }

        public class CheckInRequest
        {
            public int VolunteerId { get; set; }
        }

        // GET: /Dashboard/GetCheckedInCount
        [HttpGet]
        public async Task<IActionResult> GetCheckedInCount()
        {
            var seasonId = AppTime.CopenhagenToday.Year;
            var checkedIn = await _db.VolunteerCheckIns
                .CountAsync(c => c.SeasonId == seasonId && c.CheckedOutAt == null);
            var inPit = await _db.VolunteerCheckIns
                .CountAsync(c => c.SeasonId == seasonId && c.CheckedOutAt == null && c.CurrentLocation == "Pit");
            return Json(new { count = checkedIn, pitCount = inPit });
        }

        // GET: /Dashboard/GetNoShowCount
        [HttpGet]
        public async Task<IActionResult> GetNoShowCount()
        {
            var now = AppTime.CopenhagenNow;
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            // Frivillige der allerede er checket ind i dag
            var checkedInVolunteerIds = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null)
                .Select(c => c.VolunteerId)
                .ToListAsync();

            // Vagter der starter i dag og hvor starttidspunktet er passeret
            var noShowCount = await _db.Shifts
                .Include(s => s.ShiftType)
                .Where(s => s.SeasonId == seasonId &&
                            s.ShiftType.StartTime.Year == today.Year &&
                            s.ShiftType.StartTime.Month == today.Month &&
                            s.ShiftType.StartTime.Day == today.Day &&
                            s.ShiftType.StartTime <= now &&
                            !checkedInVolunteerIds.Contains(s.VolunteerId))
                .Select(s => s.VolunteerId)
                .Distinct()
                .CountAsync();

            return Json(new { count = noShowCount });
        }

        // GET: /Dashboard/GetNoShowList?q=...
        [HttpGet]
        public async Task<IActionResult> GetNoShowList(string? q)
        {
            var now = AppTime.CopenhagenNow;
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var checkedInVolunteerIds = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null)
                .Select(c => c.VolunteerId)
                .ToListAsync();

            var noShows = await _db.Shifts
                .Include(s => s.ShiftType)
                .Include(s => s.Volunteer)
                .Where(s => s.SeasonId == seasonId &&
                            s.ShiftType.StartTime.Year == today.Year &&
                            s.ShiftType.StartTime.Month == today.Month &&
                            s.ShiftType.StartTime.Day == today.Day &&
                            s.ShiftType.StartTime <= now &&
                            !checkedInVolunteerIds.Contains(s.VolunteerId))
                .ToListAsync();

            // Én række pr. frivillig – tag den tidligste vagtstart hvis de har flere
            var grouped = noShows
                .GroupBy(s => s.VolunteerId)
                .Select(g => new
                {
                    g.First().Volunteer.Id,
                    g.First().Volunteer.Name,
                    PhoneNumber = g.First().Volunteer.PhoneNumber,
                    EarliestStart = g.Min(s => s.ShiftType.StartTime)
                });

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                grouped = grouped.Where(v => v.Name.ToLower().Contains(term));
            }

            var result = grouped.OrderBy(v => v.EarliestStart).ThenBy(v => v.Name).ToList();

            return Json(result);
        }

        // GET: /Dashboard/GetPitVolunteers?q=...
        [HttpGet]
        public async Task<IActionResult> GetPitVolunteers(string? q)
        {
            var seasonId = AppTime.CopenhagenToday.Year;

            var hasQuery = !string.IsNullOrWhiteSpace(q);

            // Hent alle checkede ind (ikke udcheckede)
            var allCheckIns = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckedOutAt == null)
                .Include(c => c.Volunteer)
                .ToListAsync();

            // Filtrer til kun Pit når der ikke søges
            IEnumerable<VolunteerCheckIn> filtered = hasQuery
                ? allCheckIns
                : allCheckIns.Where(c => c.CurrentLocation == "Pit");

            if (hasQuery)
            {
                var term = q!.Trim().ToLower();
                filtered = filtered.Where(c =>
                    c.Volunteer.Name.ToLower().Contains(term) ||
                    (c.Volunteer.Email != null && c.Volunteer.Email.ToLower().Contains(term)) ||
                    (c.Volunteer.PhoneNumber != null && c.Volunteer.PhoneNumber.ToLower().Contains(term)));
            }

            var filteredList = filtered.ToList();

            // Hent seneste Move/CheckIn log for pit-frivillige — bruges til alarm-timing
            var checkInIds = filteredList.Select(c => c.Id).ToList();
            var arrivalLogs = await _db.VolunteerLocationLogs
                .Where(l => checkInIds.Contains(l.CheckInId) && (l.EventType == "Move" || l.EventType == "CheckIn"))
                .GroupBy(l => l.CheckInId)
                .Select(g => new
                {
                    CheckInId = g.Key,
                    ArrivedAt = g.OrderByDescending(l => l.OccurredAt).First().OccurredAt
                })
                .ToListAsync();

            var arrivalByCheckInId = arrivalLogs.ToDictionary(x => x.CheckInId, x => x.ArrivedAt);

            var result = filteredList
                .Select(c => new
                {
                    c.VolunteerId,
                    Name = c.Volunteer.Name,
                    Email = c.Volunteer.Email,
                    PhoneNumber = c.Volunteer.PhoneNumber,
                    // Tidspunkt for hvornår de senest ankom til Pit (fallback: original check-in)
                    ArrivedAtPit = arrivalByCheckInId.TryGetValue(c.Id, out var t) ? t : c.CheckedInAt,
                    CurrentLocation = c.CurrentLocation
                })
                .OrderBy(v => v.Name)
                .ToList();

            return Json(result);
        }

        // GET: /Dashboard/GetVolunteerHistory?volunteerId=...
        // Bevægelseshistorik for en frivilligs aktuelle check-in-session — bruges til
        // hover-info på dashboardet (hvornår kom de til hver post, og hvor længe de var der).
        [HttpGet]
        public async Task<IActionResult> GetVolunteerHistory(int volunteerId)
        {
            var seasonId = AppTime.CopenhagenToday.Year;

            var checkIn = await _db.VolunteerCheckIns
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == volunteerId && c.CheckedOutAt == null);

            if (checkIn == null)
                return Json(new { hasCheckIn = false });

            var logs = await _db.VolunteerLocationLogs
                .Where(l => l.CheckInId == checkIn.Id && l.EventType != "CheckOut")
                .OrderBy(l => l.OccurredAt)
                .ToListAsync();

            var now = AppTime.Now;
            var history = logs.Select((entry, i) =>
            {
                var end = i + 1 < logs.Count ? logs[i + 1].OccurredAt : now;
                var minutes = Math.Max(0, (int)Math.Round((end - entry.OccurredAt).TotalMinutes));
                return new
                {
                    location = entry.Location,
                    occurredAt = entry.OccurredAt,
                    durationMinutes = minutes,
                    isCurrent = i == logs.Count - 1
                };
            }).ToList();

            return Json(new { hasCheckIn = true, history });
        }

        // POST: /Dashboard/MoveVolunteer  — flyt frivillig mellem Pit og post eller mellem poster
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveVolunteer([FromBody] MoveVolunteerRequest request)
        {
            var seasonId = AppTime.CopenhagenToday.Year;

            var checkIn = await _db.VolunteerCheckIns
                .Include(c => c.Volunteer)
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == request.VolunteerId && c.CheckedOutAt == null);

            if (checkIn == null)
                return Json(new { success = false, message = "Frivillig er ikke checket ind." });

            var from = checkIn.CurrentLocation;
            var to = request.TargetLocation?.Trim();

            if (string.IsNullOrWhiteSpace(to))
                return Json(new { success = false, message = "Ugyldig destination." });

            if (from == to)
                return Json(new { success = true, message = "Ingen ændring." });

            checkIn.CurrentLocation = to;
            var moveTime = AppTime.Now;
            _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
            {
                CheckInId = checkIn.Id,
                VolunteerId = request.VolunteerId,
                SeasonId = seasonId,
                EventType = "Move",
                Location = to,
                OccurredAt = moveTime
            });

            await _db.SaveChangesAsync();

            await SendTemplatedSmsAsync(SmsTemplateType.Moved, checkIn.Volunteer.Id, checkIn.Volunteer.Name, moveTime, fraPost: from, tilPost: to);

            return Json(new { success = true, message = $"{checkIn.Volunteer.Name} flyttet til {to}." });
        }

        public class MoveVolunteerRequest
        {
            public int VolunteerId { get; set; }
            public string TargetLocation { get; set; } = string.Empty;
        }

        // POST: /Dashboard/QrScanPit – check ind eller flyt frivillig til Pit via QR-scanning
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QrScanPit([FromBody] QrScanPitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
                return Json(new { result = "error", message = "Ugyldig QR kode." });

            var seasonId = AppTime.CopenhagenToday.Year;

            var volunteer = await _db.Volunteers
                .FirstOrDefaultAsync(v => v.Key == request.Key.Trim() && v.SeasonId == seasonId);

            if (volunteer == null)
                return Json(new { result = "notfound", message = $"Ingen frivillig fundet med nøgle \"{request.Key}\"." });

            // Find en åben session (ikke udchecket) – der kan godt være tidligere udcheckede sessioner
            var existing = await _db.VolunteerCheckIns
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == volunteer.Id && c.CheckedOutAt == null);

            var now = AppTime.Now;

            if (existing == null)
            {
                // Ikke checket ind – opret check-in direkte i Pit
                var checkIn = new VolunteerCheckIn
                {
                    SeasonId = seasonId,
                    VolunteerId = volunteer.Id,
                    CheckInDate = AppTime.CopenhagenToday,
                    CheckedInAt = now,
                    CurrentLocation = "Pit"
                };
                _db.VolunteerCheckIns.Add(checkIn);
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

                await SendTemplatedSmsAsync(SmsTemplateType.CheckIn, volunteer.Id, volunteer.Name, now, post: "Pit");

                return Json(new { result = "checkedin", message = $"{volunteer.Name} er nu checket ind i Pitten." });
            }

            if (existing.CurrentLocation == "Pit")
                return Json(new { result = "alreadyinpit", message = $"{volunteer.Name} er allerede i Pitten." });

            // Checket ind andetsteds – flyt til Pit
            var from = existing.CurrentLocation;
            existing.CurrentLocation = "Pit";
            _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
            {
                CheckInId = existing.Id,
                VolunteerId = volunteer.Id,
                SeasonId = seasonId,
                EventType = "Move",
                Location = "Pit",
                OccurredAt = now
            });
            await _db.SaveChangesAsync();

            await SendTemplatedSmsAsync(SmsTemplateType.Moved, volunteer.Id, volunteer.Name, now, fraPost: from, tilPost: "Pit");

            return Json(new { result = "moved", message = $"{volunteer.Name} er flyttet fra {from} til Pitten.", volunteerId = volunteer.Id });
        }

        public class QrScanPitRequest
        {
            public string Key { get; set; } = string.Empty;
        }

        public record SetSettingRequest(string Key, string? Value);

        // POST: /Dashboard/ScheduleFutureMove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleFutureMove([FromBody] ScheduleFutureMoveRequest req)
        {
            if (req.VolunteerId <= 0 || string.IsNullOrWhiteSpace(req.TargetLocation) || req.DelayMinutes <= 0)
                return Json(new { success = false, message = "Ugyldige parametre." });

            var seasonId = AppTime.CopenhagenToday.Year;

            var volunteer = await _db.Volunteers.FindAsync(req.VolunteerId);
            if (volunteer == null)
                return Json(new { success = false, message = "Frivillig ikke fundet." });

            var checkIn = await _db.VolunteerCheckIns
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == req.VolunteerId && c.CheckedOutAt == null);
            if (checkIn == null)
                return Json(new { success = false, message = $"{volunteer.Name} er ikke checket ind." });

            // Annullér eventuel eksisterende planlagt flytning
            var existing = await _db.ScheduledMoves
                .Where(m => m.VolunteerId == req.VolunteerId && !m.IsCancelled && m.ExecutedAt == null)
                .ToListAsync();
            existing.ForEach(m => m.IsCancelled = true);

            var now = AppTime.Now;
            var scheduled = new ScheduledMove
            {
                SeasonId = seasonId,
                VolunteerId = req.VolunteerId,
                TargetLocation = req.TargetLocation.Trim(),
                ScheduledAt = now.AddMinutes(req.DelayMinutes),
                CreatedByUser = User.Identity?.Name ?? "Ukendt",
                CreatedAt = now
            };
            _db.ScheduledMoves.Add(scheduled);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"{volunteer.Name} flyttes til \"{req.TargetLocation}\" om {req.DelayMinutes} min.", scheduledAt = scheduled.ScheduledAt });
        }

        public class ScheduleFutureMoveRequest
        {
            public int VolunteerId { get; set; }
            public string TargetLocation { get; set; } = string.Empty;
            public int DelayMinutes { get; set; }
        }

        // GET: /Dashboard/GetScheduledMove?volunteerId=...
        [HttpGet]
        public async Task<IActionResult> GetScheduledMove(int volunteerId)
        {
            var pending = await _db.ScheduledMoves
                .Where(m => m.VolunteerId == volunteerId && !m.IsCancelled && m.ExecutedAt == null)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            if (pending == null)
                return Json(new { hasPending = false });

            var minsLeft = (int)Math.Ceiling((pending.ScheduledAt - AppTime.Now).TotalMinutes);
            return Json(new { hasPending = true, id = pending.Id, targetLocation = pending.TargetLocation, scheduledAt = pending.ScheduledAt, minsLeft });
        }

        // POST: /Dashboard/CancelScheduledMove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelScheduledMove([FromBody] CancelScheduledMoveRequest req)
        {
            var move = await _db.ScheduledMoves.FindAsync(req.MoveId);
            if (move == null)
                return Json(new { success = false, message = "Planlagt flytning ikke fundet." });

            if (move.IsCancelled || move.ExecutedAt != null)
                return Json(new { success = false, message = "Flytningen er allerede udført eller annulleret." });

            move.IsCancelled = true;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Planlagt flytning annulleret." });
        }

        public class CancelScheduledMoveRequest
        {
            public int MoveId { get; set; }
        }

        // GET: /Dashboard/GetCameraPreference
        [HttpGet]
        public async Task<IActionResult> GetCameraPreference()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var pref = await _db.UserCameraPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref == null)
                return Json(new { deviceId = (string?)null, deviceFingerprint = (string?)null });

            return Json(new { deviceId = pref.DeviceId, deviceFingerprint = pref.DeviceFingerprint });
        }

        // POST: /Dashboard/SaveCameraPreference
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCameraPreference([FromBody] SaveCameraPreferenceRequest req)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.DeviceFingerprint))
                return Json(new { success = false, message = "Manglende data." });

            var pref = await _db.UserCameraPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref == null)
            {
                pref = new UserCameraPreference { UserId = userId };
                _db.UserCameraPreferences.Add(pref);
            }

            pref.DeviceId = req.DeviceId;
            pref.DeviceFingerprint = req.DeviceFingerprint;
            pref.UpdatedAt = AppTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: /Dashboard/ClearCameraPreference
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCameraPreference()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var pref = await _db.UserCameraPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (pref != null)
            {
                _db.UserCameraPreferences.Remove(pref);
                await _db.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        public record SaveCameraPreferenceRequest(string DeviceId, string DeviceFingerprint);

        // GET: /Dashboard/StateHash — returnerer et let fingerprint af den nuværende driftsstate.
        // Bruges af klientens background-poller til at detektere ændringer uden at hente alle data.
        [HttpGet]
        public async Task<IActionResult> StateHash()
        {
            var seasonId = AppTime.CopenhagenToday.Year;

            var checkInCount = await _db.VolunteerCheckIns
                .CountAsync(c => c.SeasonId == seasonId && c.CheckedOutAt == null);

            var lastLogTick = await _db.VolunteerLocationLogs
                .Where(l => l.SeasonId == seasonId)
                .OrderByDescending(l => l.OccurredAt)
                .Select(l => (DateTime?)l.OccurredAt)
                .FirstOrDefaultAsync();

            var postCount = await _db.Posts.CountAsync(p => p.SeasonId == seasonId);

            // Inkluder positioner, navne og alarmindstillinger så enhver post-ændring detekteres
            var posts = await _db.Posts
                .Where(p => p.SeasonId == seasonId)
                .Select(p => new { p.Id, p.ColumnIndex, p.SortOrder, p.Name, p.AlarmAfterMinutes })
                .ToListAsync();

            var postPositionHash = posts.Sum(p => (long)(p.Id * 31 + p.ColumnIndex * 7 + p.SortOrder));
            var postMetaHash = posts.Aggregate(0, (acc, p) =>
                HashCode.Combine(acc, p.Name, p.AlarmAfterMinutes ?? 0));

            // Inkluder sms-flow-tilstanden (og evt. automatisk sluk) så alle åbne faner opdager ændringer fra andre
            var smsFlowSetting = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == SmsFlowSetting.Key);
            var smsFlowAutoOff = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == SmsFlowSetting.AutoOffAtKey);

            // Enkel deterministisk hash – billig at beregne
            var raw = $"{checkInCount}|{lastLogTick?.Ticks ?? 0}|{postCount}|{postPositionHash}|{postMetaHash}|{smsFlowSetting?.Value}|{smsFlowAutoOff?.Value}";
            var hash = raw.GetHashCode().ToString("X8");

            return Json(new { hash });
        }

        // POST: /Dashboard/CheckOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut([FromBody] CheckInRequest request)
        {
            var seasonId = AppTime.CopenhagenToday.Year;

            var volunteer = await _db.Volunteers.FindAsync(request.VolunteerId);
            if (volunteer == null)
                return Json(new { success = false, message = "Frivillig ikke fundet." });

            var existing = await _db.VolunteerCheckIns
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == request.VolunteerId && c.CheckedOutAt == null);

            if (existing == null)
                return Json(new { success = false, message = $"{volunteer.Name} er ikke checket ind." });

            var now = AppTime.Now;
            var checkedOutFrom = existing.CurrentLocation;

            // Hvis personen ikke er i pitten
            if (existing.CurrentLocation != "Pit")
            {
                existing.CurrentLocation = "Pit";
                _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
                {
                    CheckInId   = existing.Id,
                    VolunteerId = request.VolunteerId,
                    SeasonId    = seasonId,
                    EventType   = "Move",
                    Location    = "Pit",
                    OccurredAt  = now
                });
            }

            // Log checkout – Location = null jf. modellens konvention
            _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
            {
                CheckInId   = existing.Id,
                VolunteerId = request.VolunteerId,
                SeasonId    = seasonId,
                EventType   = "CheckOut",
                Location    = null,
                OccurredAt  = now
            });

            existing.CheckedOutAt = now;

            // Slet al GPS-data for den frivillige ved checkout
            await _db.VolunteerGpsLogs
                .Where(l => l.VolunteerId == request.VolunteerId && l.SeasonId == seasonId)
                .ExecuteDeleteAsync();

            await _db.SaveChangesAsync();

            await SendTemplatedSmsAsync(SmsTemplateType.CheckOut, volunteer.Id, volunteer.Name, now, post: checkedOutFrom);

            return Json(new { success = true, message = $"{volunteer.Name} er nu checket ud." });
        }

        // GET /Dashboard/GetSmsTemplates
        [HttpGet]
        public async Task<IActionResult> GetSmsTemplates()
        {
            var seasonId = AppTime.CurrentSeason;
            var saved = await _db.SmsTemplates
                .Where(t => t.SeasonId == seasonId)
                .ToDictionaryAsync(t => t.Type, t => t.Body);

            return Json(new
            {
                checkIn = saved.GetValueOrDefault(SmsTemplateType.CheckIn, SmsTemplateDefaults.CheckIn),
                checkOut = saved.GetValueOrDefault(SmsTemplateType.CheckOut, SmsTemplateDefaults.CheckOut),
                moved = saved.GetValueOrDefault(SmsTemplateType.Moved, SmsTemplateDefaults.Moved)
            });
        }

        // POST /Dashboard/SaveSmsTemplate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSmsTemplate([FromBody] SaveSmsTemplateRequest req)
        {
            if (!Enum.TryParse<SmsTemplateType>(req.Type, ignoreCase: true, out var type))
                return Json(new { success = false, message = "Ugyldig skabelontype." });

            if (string.IsNullOrWhiteSpace(req.Body))
                return Json(new { success = false, message = "Teksten må ikke være tom." });

            var seasonId = AppTime.CurrentSeason;
            var template = await _db.SmsTemplates
                .FirstOrDefaultAsync(t => t.SeasonId == seasonId && t.Type == type);

            if (template == null)
            {
                template = new SmsTemplate { SeasonId = seasonId, Type = type };
                _db.SmsTemplates.Add(template);
            }

            template.Body = req.Body.Trim();
            template.UpdatedAt = AppTime.Now;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class SaveSmsTemplateRequest
        {
            public string Type { get; set; } = "";
            public string Body { get; set; } = "";
        }
    }
}

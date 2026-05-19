using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: /Home/CheckInSearch?q=...&date=yyyy-MM-dd
        [HttpGet]
        public async Task<IActionResult> CheckInSearch(string? q, string? date)
        {
            var today = date != null && DateOnly.TryParse(date, out var d) ? d : AppTime.CopenhagenToday;
            var seasonId = today.Year;

            // Frivillige der allerede er checket ind i dag (åben session)
            var alreadyCheckedInIds = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null)
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

        // POST: /Home/CheckIn
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

            return Json(new { success = true, message = $"{volunteer.Name} er nu checket ind i Pitten." });
        }

        public class CheckInRequest
        {
            public int VolunteerId { get; set; }
        }

        // GET: /Home/GetCheckedInCount
        [HttpGet]
        public async Task<IActionResult> GetCheckedInCount()
        {
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;
            var checkedIn = await _db.VolunteerCheckIns
                .CountAsync(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null);
            var inPit = await _db.VolunteerCheckIns
                .CountAsync(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null && c.CurrentLocation == "Pit");
            return Json(new { count = checkedIn, pitCount = inPit });
        }

        // GET: /Home/GetNoShowCount
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

        // GET: /Home/GetNoShowList?q=...
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

        // GET: /Home/GetPitVolunteers?q=...
        [HttpGet]
        public async Task<IActionResult> GetPitVolunteers(string? q)
        {
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var hasQuery = !string.IsNullOrWhiteSpace(q);

            // Hent alle checkede ind i dag (ikke udcheckede)
            var allCheckIns = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null)
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

        // POST: /Home/MoveVolunteer  — flyt frivillig mellem Pit og post eller mellem poster
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveVolunteer([FromBody] MoveVolunteerRequest request)
        {
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var checkIn = await _db.VolunteerCheckIns
                .Include(c => c.Volunteer)
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == request.VolunteerId && c.CheckInDate == today && c.CheckedOutAt == null);

            if (checkIn == null)
                return Json(new { success = false, message = "Frivillig er ikke checket ind." });

            var from = checkIn.CurrentLocation;
            var to = request.TargetLocation?.Trim();

            if (string.IsNullOrWhiteSpace(to))
                return Json(new { success = false, message = "Ugyldig destination." });

            if (from == to)
                return Json(new { success = true, message = "Ingen ændring." });

            checkIn.CurrentLocation = to;
            _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
            {
                CheckInId = checkIn.Id,
                VolunteerId = request.VolunteerId,
                SeasonId = seasonId,
                EventType = "Move",
                Location = to,
                OccurredAt = AppTime.Now
            });

            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"{checkIn.Volunteer.Name} flyttet til {to}." });
        }

        public class MoveVolunteerRequest
        {
            public int VolunteerId { get; set; }
            public string TargetLocation { get; set; } = string.Empty;
        }

        // POST: /Home/QrScanPit – check ind eller flyt frivillig til Pit via QR-scanning
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QrScanPit([FromBody] QrScanPitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
                return Json(new { result = "error", message = "Ugyldig QR kode." });

            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var volunteer = await _db.Volunteers
                .FirstOrDefaultAsync(v => v.Key == request.Key.Trim() && v.SeasonId == seasonId);

            if (volunteer == null)
                return Json(new { result = "notfound", message = $"Ingen frivillig fundet med nøgle \"{request.Key}\"." });

            // Find en åben session (ikke udchecket) – der kan godt være tidligere udcheckede sessioner i dag
            var existing = await _db.VolunteerCheckIns
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == volunteer.Id && c.CheckInDate == today && c.CheckedOutAt == null);

            var now = AppTime.Now;

            if (existing == null)
            {
                // Ikke checket ind – opret check-in direkte i Pit
                var checkIn = new VolunteerCheckIn
                {
                    SeasonId = seasonId,
                    VolunteerId = volunteer.Id,
                    CheckInDate = today,
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

            return Json(new { result = "moved", message = $"{volunteer.Name} er flyttet fra {from} til Pitten.", volunteerId = volunteer.Id });
        }

        public class QrScanPitRequest
        {
            public string Key { get; set; } = string.Empty;
        }

        // GET: /Home/StateHash — returnerer et let fingerprint af den nuværende driftsstate.
        // Bruges af klientens background-poller til at detektere ændringer uden at hente alle data.
        [HttpGet]
        public async Task<IActionResult> StateHash()
        {
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var checkInCount = await _db.VolunteerCheckIns
                .CountAsync(c => c.SeasonId == seasonId && c.CheckInDate == today && c.CheckedOutAt == null);

            var lastLogTick = await _db.VolunteerLocationLogs
                .Where(l => l.SeasonId == seasonId)
                .OrderByDescending(l => l.OccurredAt)
                .Select(l => (DateTime?)l.OccurredAt)
                .FirstOrDefaultAsync();

            var postCount = await _db.Posts.CountAsync(p => p.SeasonId == seasonId);

            // Inkluder en checksum af posternes positioner så flytning af poster også detekteres
            var postPositionHash = await _db.Posts
                .Where(p => p.SeasonId == seasonId)
                .Select(p => p.Id * 31 + p.ColumnIndex * 7 + p.SortOrder)
                .SumAsync(x => (long)x);

            // Enkel deterministisk hash – billig at beregne
            var raw = $"{checkInCount}|{lastLogTick?.Ticks ?? 0}|{postCount}|{postPositionHash}";
            var hash = raw.GetHashCode().ToString("X8");

            return Json(new { hash });
        }

        // POST: /Home/CheckOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut([FromBody] CheckInRequest request)
        {
            var today = AppTime.CopenhagenToday;
            var seasonId = today.Year;

            var volunteer = await _db.Volunteers.FindAsync(request.VolunteerId);
            if (volunteer == null)
                return Json(new { success = false, message = "Frivillig ikke fundet." });

            var existing = await _db.VolunteerCheckIns
                .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.VolunteerId == request.VolunteerId && c.CheckInDate == today && c.CheckedOutAt == null);

            if (existing == null)
                return Json(new { success = false, message = $"{volunteer.Name} er ikke checket ind." });

            var now = AppTime.Now;

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
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"{volunteer.Name} er nu checket ud." });
        }
    }
}


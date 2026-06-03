using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers;

[Authorize]
[Route("Post")]
public class PostController : Controller
{
    private readonly ApplicationDbContext _db;

    public PostController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /Post/GetAll
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        var seasonId = AppTime.CurrentSeason;
        var posts = await _db.Posts
            .Where(p => p.SeasonId == seasonId)
            .OrderBy(p => p.ColumnIndex)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync();

        // Hent frivillige per post (CurrentLocation matcher postens navn)
        var checkIns = await _db.VolunteerCheckIns
            .Where(c => c.SeasonId == seasonId && c.CheckedOutAt == null && c.CurrentLocation != "Pit")
            .Include(c => c.Volunteer)
            .ToListAsync();

        var checkInIds = checkIns.Select(c => c.Id).ToList();

        // Hent det seneste tidspunkt den frivillige ankom til sin nuværende location
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

        var result = posts.Select(p => new
        {
            p.Id,
            p.Name,
            p.ColumnIndex,
            p.SortOrder,
            p.AlarmAfterMinutes,
            Volunteers = checkIns
                .Where(c => c.CurrentLocation == p.Name)
                .Select(c => new
                {
                    CheckInId = c.Id,
                    VolunteerId = c.VolunteerId,
                    Name = c.Volunteer.Name,
                    // ArrivedAtPost: hvornår de senest ankom til denne post
                    ArrivedAtPost = arrivalByCheckInId.TryGetValue(c.Id, out var t) ? t : c.CheckedInAt
                })
                .OrderBy(v => v.Name)
                .ToList()
        });

        return Json(result);
    }

    // POST /Post/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Json(new { success = false, message = "Navn er påkrævet." });

        if (req.ColumnIndex < 1 || req.ColumnIndex > 5)
            return Json(new { success = false, message = "Ugyldig spalte." });

        var seasonId = AppTime.CurrentSeason;

        var exists = await _db.Posts.AnyAsync(p => p.SeasonId == seasonId && p.Name == req.Name.Trim());
        if (exists)
            return Json(new { success = false, message = $"En post med navnet \"{req.Name.Trim()}\" findes allerede." });

        var maxSort = await _db.Posts
            .Where(p => p.SeasonId == seasonId && p.ColumnIndex == req.ColumnIndex)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;

        var post = new Post
        {
            SeasonId = seasonId,
            Name = req.Name.Trim(),
            ColumnIndex = req.ColumnIndex,
            SortOrder = maxSort + 1,
            AlarmAfterMinutes = req.AlarmAfterMinutes
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = $"Post \"{post.Name}\" oprettet.", post = new { post.Id, post.Name, post.ColumnIndex, post.SortOrder, post.AlarmAfterMinutes } });
    }

    // POST /Post/UpdateColumn
    [HttpPost("UpdateColumn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateColumn([FromBody] UpdateColumnRequest req)
    {
        if (req.ColumnIndex < 1 || req.ColumnIndex > 5)
            return Json(new { success = false, message = "Ugyldig spalte." });

        var post = await _db.Posts.FindAsync(req.PostId);
        if (post == null)
            return Json(new { success = false, message = "Post ikke fundet." });

        var maxSort = await _db.Posts
            .Where(p => p.SeasonId == post.SeasonId && p.ColumnIndex == req.ColumnIndex && p.Id != post.Id)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;

        post.ColumnIndex = req.ColumnIndex;
        post.SortOrder = req.SortOrder.HasValue ? req.SortOrder.Value : maxSort + 1;
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }

    // POST /Post/Update
    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([FromBody] UpdatePostRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Json(new { success = false, message = "Navn er påkrævet." });

        var post = await _db.Posts.FindAsync(req.PostId);
        if (post == null)
            return Json(new { success = false, message = "Post ikke fundet." });

        var seasonId = AppTime.CurrentSeason;
        var newName = req.Name.Trim();

        // Tjek om et andet post allerede bruger det nye navn
        var nameConflict = await _db.Posts.AnyAsync(p => p.SeasonId == seasonId && p.Name == newName && p.Id != req.PostId);
        if (nameConflict)
            return Json(new { success = false, message = $"En post med navnet \"{newName}\" findes allerede." });

        var oldName = post.Name;

        // Opdater CurrentLocation på aktive check-ins hvis navnet ændrer sig
        if (oldName != newName)
        {
            var checkIns = await _db.VolunteerCheckIns
                .Where(c => c.SeasonId == seasonId && c.CheckedOutAt == null && c.CurrentLocation == oldName)
                .ToListAsync();
            foreach (var ci in checkIns)
                ci.CurrentLocation = newName;
        }

        post.Name = newName;
        post.AlarmAfterMinutes = req.AlarmAfterMinutes;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = $"Post opdateret." });
    }

    // POST /Post/Delete
    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromBody] DeletePostRequest req)
    {
        var post = await _db.Posts.FindAsync(req.PostId);
        if (post == null)
            return Json(new { success = false, message = "Post ikke fundet." });

        // Flyt frivillige der er på posten tilbage til Pit
        var seasonId = AppTime.CurrentSeason;
        var checkIns = await _db.VolunteerCheckIns
            .Where(c => c.SeasonId == seasonId && c.CheckedOutAt == null && c.CurrentLocation == post.Name)
            .ToListAsync();

        var now = AppTime.Now;
        foreach (var ci in checkIns)
        {
            ci.CurrentLocation = "Pit";
            _db.VolunteerLocationLogs.Add(new VolunteerLocationLog
            {
                CheckInId = ci.Id,
                VolunteerId = ci.VolunteerId,
                SeasonId = seasonId,
                EventType = "Move",
                Location = "Pit",
                OccurredAt = now
            });
        }

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = $"Post \"{post.Name}\" slettet. {checkIns.Count} frivillige flyttet tilbage til Pitten." });
    }

    // POST /Post/Reorder
    [HttpPost("Reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return Json(new { success = false, message = "Ingen data." });

        var seasonId = AppTime.CurrentSeason;
        var ids = req.Items.Select(i => i.PostId).ToList();
        var posts = await _db.Posts
            .Where(p => p.SeasonId == seasonId && ids.Contains(p.Id))
            .ToListAsync();

        foreach (var item in req.Items)
        {
            var post = posts.FirstOrDefault(p => p.Id == item.PostId);
            if (post != null) post.SortOrder = item.SortOrder;
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    public class ReorderItem
    {
        public int PostId { get; set; }
        public int SortOrder { get; set; }
    }

    public class ReorderRequest
    {
        public List<ReorderItem> Items { get; set; } = new();
    }

    public class CreatePostRequest
    {
        public string Name { get; set; } = string.Empty;
        public int ColumnIndex { get; set; } = 1;
        public int? AlarmAfterMinutes { get; set; }
    }

    public class UpdateColumnRequest
    {
        public int PostId { get; set; }
        public int ColumnIndex { get; set; }
        public int? SortOrder { get; set; }
    }

    public class UpdatePostRequest
    {
        public int PostId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? AlarmAfterMinutes { get; set; }
    }

    public class DeletePostRequest
    {
        public int PostId { get; set; }
    }
}

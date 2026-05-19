using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Controllers;

[Authorize]
public class BeskederController(
    ApplicationDbContext db,
    UserManager<AppUser> userManager,
    IWebHostEnvironment env) : Controller
{
    private static int CurrentSeason => AppTime.CurrentSeason;

    private string AttachmentDirectory =>
        Path.Combine(env.ContentRootPath, "App_files", "beskeder");

    // ── Index ─────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var season = CurrentSeason;
        var vm = new BeskederIndexViewModel
        {
            UnreadCount = await db.Messages.CountAsync(m => m.SeasonId == season && !m.IsRead && !m.IsDeleted),
            ReadCount   = await db.Messages.CountAsync(m => m.SeasonId == season && m.IsRead && !m.IsDeleted),
            TaskCount   = await db.MessageTasks.CountAsync(t => t.Message.SeasonId == season && t.Status != MessageTaskStatus.Udført)
        };
        return View(vm);
    }

    // ── GetCounts ─────────────────────────────────────────────────
    public async Task<IActionResult> GetCounts()
    {
        var season = CurrentSeason;
        return Json(new
        {
            unreadCount = await db.Messages.CountAsync(m => m.SeasonId == season && !m.IsRead && !m.IsDeleted),
            readCount   = await db.Messages.CountAsync(m => m.SeasonId == season && m.IsRead && !m.IsDeleted),
            taskCount   = await db.MessageTasks.CountAsync(t => t.Message.SeasonId == season && t.Status != MessageTaskStatus.Udført)
        });
    }

    // ── Partials ──────────────────────────────────────────────────
    public async Task<IActionResult> UnreadPartial(string q = "", int page = 1, int pageSize = 10)
        => await BuildMessagePartial(q, page, pageSize, isRead: false, "_UnreadPartial");

    public async Task<IActionResult> ReadPartial(string q = "", int page = 1, int pageSize = 10)
        => await BuildMessagePartial(q, page, pageSize, isRead: true, "_ReadPartial");

    public async Task<IActionResult> UnreadSearch(string q = "", int page = 1, int pageSize = 10)
        => await BuildMessagePartial(q, page, pageSize, isRead: false, "_UnreadPartial");

    public async Task<IActionResult> ReadSearch(string q = "", int page = 1, int pageSize = 10)
        => await BuildMessagePartial(q, page, pageSize, isRead: true, "_ReadPartial");

    private async Task<IActionResult> BuildMessagePartial(string q, int page, int pageSize, bool isRead, string view)
    {
        var season = CurrentSeason;
        var query = db.Messages
            .Where(m => m.SeasonId == season && m.IsRead == isRead && !m.IsDeleted)
            .Include(m => m.Volunteer)
            .Include(m => m.Attachments)
            .Include(m => m.Tasks)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(m =>
                m.Subject.ToLower().Contains(ql) ||
                m.Volunteer.Name.ToLower().Contains(ql) ||
                m.Volunteer.Key.ToLower().Contains(ql));
        }

        query = query.OrderByDescending(m => m.SentAt);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var messages = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var rows = messages.Select(m => new MessageRowViewModel
        {
            Id             = m.Id,
            VolunteerName  = m.Volunteer.Name,
            VolunteerKey   = m.Volunteer.Key,
            Subject        = m.Subject,
            BodyPreview    = m.Body.Length > 80 ? m.Body[..80] + "…" : m.Body,
            IsRead         = m.IsRead,
            SentAt         = m.SentAt,
            ReadAt         = m.ReadAt,
            Direction      = m.Direction,
            AttachmentCount = m.Attachments.Count,
            TaskCount      = m.Tasks.Count
        }).ToList();

        if (isRead)
        {
            var rvm = new ReadMessagesViewModel { Messages = rows, Page = page, PageSize = pageSize, TotalCount = totalCount, Query = q };
            return PartialView(view, rvm);
        }
        else
        {
            var uvm = new UnreadMessagesViewModel { Messages = rows, Page = page, PageSize = pageSize, TotalCount = totalCount, Query = q };
            return PartialView(view, uvm);
        }
    }

    // ── Opgaver partial ───────────────────────────────────────────
    public async Task<IActionResult> TasksPartial(string q = "", int page = 1, int pageSize = 10)
        => await BuildTasksPartial(q, page, pageSize);

    public async Task<IActionResult> TasksSearch(string q = "", int page = 1, int pageSize = 10)
        => await BuildTasksPartial(q, page, pageSize);

    private async Task<IActionResult> BuildTasksPartial(string q, int page, int pageSize)
    {
        var season = CurrentSeason;
        var query = db.MessageTasks
            .Where(t => t.Message.SeasonId == season)
            .Include(t => t.Message)
                .ThenInclude(m => m.Volunteer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(ql) ||
                t.Message.Volunteer.Name.ToLower().Contains(ql) ||
                t.Message.Subject.ToLower().Contains(ql));
        }

        query = query.OrderBy(t => t.Status).ThenBy(t => t.DueDate).ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var tasks = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var vm = new TasksViewModel
        {
            Tasks = tasks.Select(t => new TaskRowViewModel
            {
                Id             = t.Id,
                MessageId      = t.MessageId,
                Title          = t.Title,
                Description    = t.Description,
                Status         = t.Status,
                CreatedAt      = t.CreatedAt,
                DueDate        = t.DueDate,
                VolunteerName  = t.Message.Volunteer.Name,
                VolunteerKey   = t.Message.Volunteer.Key,
                MessageSubject = t.Message.Subject
            }).ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
            Query      = q
        };

        return PartialView("_TasksPartial", vm);
    }

    // ── Opret besked – formular ────────────────────────────────────
    public async Task<IActionResult> GetCreateForm()
    {
        var vm = new CreateMessageViewModel
        {
            AvailableVolunteers = await db.Volunteers
                .Where(v => v.SeasonId == CurrentSeason)
                .OrderBy(v => v.Name)
                .ToListAsync()
        };
        return PartialView("_CreateMessageModal", vm);
    }

    // ── Opret besked – post ────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMessage([FromForm] CreateMessageViewModel vm)
    {
        if (vm.VolunteerId == 0 || string.IsNullOrWhiteSpace(vm.Subject) || string.IsNullOrWhiteSpace(vm.Body))
            return Json(new { success = false, message = "Udfyld alle påkrævede felter." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        var msg = new Message
        {
            SeasonId      = CurrentSeason,
            VolunteerId   = vm.VolunteerId,
            SentByUserId  = user.Id,
            Direction     = MessageDirection.Outbound,
            Subject       = vm.Subject.Trim(),
            Body          = vm.Body.Trim(),
            IsRead        = true,   // Koordinator har selv skrevet den – allerede "læst" for koordinator
            ReadAt        = DateTime.Now,
            VolunteerOpenedAt = null, // Frivillig har ikke set den endnu
            SentAt        = DateTime.Now
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = "Besked oprettet." });
    }

    // ── Vis besked detalje ────────────────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var msg = await db.Messages
            .Include(m => m.Volunteer)
            .Include(m => m.Attachments)
            .Include(m => m.Tasks)
            .Include(m => m.Replies)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (msg is null) return NotFound();

        // Marker som læst
        if (!msg.IsRead)
        {
            msg.IsRead = true;
            msg.ReadAt = DateTime.Now;
            await db.SaveChangesAsync();
        }

        var sender = await userManager.FindByIdAsync(msg.SentByUserId);

        // Byg svartråd
        var replies = new List<ReplyRowViewModel>();
        foreach (var r in msg.Replies.OrderBy(r => r.SentAt))
        {
            string senderName;
            if (r.SentByUserId is not null)
            {
                var u = await userManager.FindByIdAsync(r.SentByUserId);
                senderName = u?.DisplayName ?? u?.UserName ?? "Koordinator";
            }
            else
            {
                senderName = msg.Volunteer.Name;
            }
            replies.Add(new ReplyRowViewModel
            {
                Id         = r.Id,
                Body       = r.Body,
                SentAt     = r.SentAt,
                Direction  = r.Direction,
                SenderName = senderName
            });
        }

        var vm = new MessageDetailViewModel
        {
            Message    = msg,
            SentByName = sender?.DisplayName ?? sender?.UserName ?? "Ukendt",
            Replies    = replies
        };
        return PartialView("_MessageDetailModal", vm);
    }

    // ── Besvar besked (koordinator) ────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyMessage([FromForm] ReplyMessageViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Body))
            return Json(new { success = false, message = "Svar kan ikke være tomt." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        var msg = await db.Messages.FindAsync(vm.MessageId);
        if (msg is null) return Json(new { success = false, message = "Besked ikke fundet." });

        db.MessageReplies.Add(new MessageReply
        {
            MessageId    = vm.MessageId,
            SentByUserId = user.Id,
            Direction    = MessageDirection.Outbound,
            Body         = vm.Body.Trim(),
            SentAt       = DateTime.Now
        });

        // Koordinator har svaret – beskrives allerede som læst fra koordinators side.
        // IsRead røres ikke her; sættes kun false når frivillig sender nyt.
        // Sæt VolunteerOpenedAt til null så frivillig ser det som ulæst.
        msg.VolunteerOpenedAt = null;

        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Svar sendt." });
    }

    // ── Modtag svar fra frivillig (ekstern API/endpoint) ──────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VolunteerReply([FromForm] int messageId, [FromForm] string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Json(new { success = false, message = "Svar kan ikke være tomt." });

        var msg = await db.Messages.FindAsync(messageId);
        if (msg is null) return Json(new { success = false, message = "Besked ikke fundet." });

        db.MessageReplies.Add(new MessageReply
        {
            MessageId    = messageId,
            SentByUserId = null, // Frivillig har ingen AppUser
            Direction    = MessageDirection.Inbound,
            Body         = body.Trim(),
            SentAt       = DateTime.Now
        });

        // Frivillig har svaret – sæt koordinator-ulæst
        msg.IsRead = false;
        msg.ReadAt = null;

        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Svar modtaget." });
    }

    // ── Marker som ulæst ─────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnread([FromForm] int id)
    {
        var msg = await db.Messages.FindAsync(id);
        if (msg is null) return Json(new { success = false, message = "Besked ikke fundet." });
        msg.IsRead = false;
        msg.ReadAt = null;
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Markeret som ulæst." });
    }

    // ── Slet besked ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage([FromForm] int id)
    {
        var msg = await db.Messages
            .Include(m => m.Attachments)
            .Include(m => m.Tasks)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (msg is null) return Json(new { success = false, message = "Besked ikke fundet." });

        if (msg.Tasks.Count > 0)
        {
            // Soft delete – opgaver er tilknyttet
            msg.IsDeleted  = true;
            msg.DeletedAt  = DateTime.Now;
            await db.SaveChangesAsync();
            return Json(new { success = true, message = "Besked skjult (opgaver bevaret)." });
        }

        // Hard delete – ingen opgaver
        HardDeleteMessage(msg);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Besked slettet." });
    }

    private void HardDeleteMessage(Message msg)
    {
        foreach (var att in msg.Attachments)
        {
            var path = Path.Combine(AttachmentDirectory, att.StoredFileName);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        db.Messages.Remove(msg);
    }

    // ── Hent opret-opgave formular ────────────────────────────────
    public IActionResult GetCreateTaskForm(int messageId)
    {
        return PartialView("_CreateTaskModal", new CreateTaskViewModel { MessageId = messageId });
    }

    // ── Opret opgave ──────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTask([FromForm] CreateTaskViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Title))
            return Json(new { success = false, message = "Titel er påkrævet." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        var msg = await db.Messages.FindAsync(vm.MessageId);
        if (msg is null) return Json(new { success = false, message = "Besked ikke fundet." });

        DateTime? due = null;
        if (!string.IsNullOrWhiteSpace(vm.DueDate) && DateTime.TryParse(vm.DueDate, out var d))
            due = d;

        db.MessageTasks.Add(new MessageTask
        {
            MessageId       = vm.MessageId,
            Title           = vm.Title.Trim(),
            Description     = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
            Status          = MessageTaskStatus.Åben,
            CreatedAt       = DateTime.Now,
            CreatedByUserId = user.Id,
            DueDate         = due
        });
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Opgave oprettet." });
    }

    // ── Opdater opgave status ────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaskStatus([FromForm] int id, [FromForm] MessageTaskStatus status)
    {
        var task = await db.MessageTasks.FindAsync(id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        task.Status = status;
        if (status == MessageTaskStatus.Udført)
        {
            var user = await userManager.GetUserAsync(User);
            task.CompletedAt       = DateTime.Now;
            task.CompletedByUserId = user?.Id;
        }
        else
        {
            task.CompletedAt = null;
            task.CompletedByUserId = null;
        }

        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Status opdateret." });
    }

    // ── Slet opgave ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTask([FromForm] int id)
    {
        var task = await db.MessageTasks
            .Include(t => t.Message)
                .ThenInclude(m => m.Tasks)
            .Include(t => t.Message)
                .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        var msg = task.Message;
        db.MessageTasks.Remove(task);

        // Hvis beskeden er soft-deleted og det var den sidste opgave, hard-delete nu
        var remainingTasks = msg.Tasks.Count(t => t.Id != id);
        if (msg.IsDeleted && remainingTasks == 0)
        {
            HardDeleteMessage(msg);
            await db.SaveChangesAsync();
            return Json(new { success = true, message = "Opgave og tilknyttet besked slettet." });
        }

        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Opgave slettet." });
    }

    // ── Fil-download ──────────────────────────────────────────────
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var att = await db.MessageAttachments.FindAsync(id);
        if (att is null) return NotFound();
        var path = Path.Combine(AttachmentDirectory, att.StoredFileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, att.ContentType, att.OriginalFileName);
    }
}


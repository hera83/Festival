using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using web.Data;
using web.Models;
using web.Services.Sms;
using web.Utils;

namespace web.Controllers;

[Authorize]
public class BeskederController(
    ApplicationDbContext db,
    UserManager<AppUser> userManager,
    IWebHostEnvironment env,
    ISmsMessageLogService smsMessageLogService) : Controller
{
    private const string TaskMetaMarker = "\n\n__FV_TASK_META__\n";

    private static int CurrentSeason => AppTime.CurrentSeason;

    private string AttachmentDirectory =>
        Path.Combine(env.ContentRootPath, "App_files", "beskeder");

    // ── Index ─────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var season = CurrentSeason;
        var (smsUnread, smsRead) = await GetSmsThreadCountsAsync(season);
        var vm = new BeskederIndexViewModel
        {
            UnreadCount = await db.Messages.CountAsync(m => m.SeasonId == season && !m.IsRead && !m.IsDeleted) + smsUnread,
            ReadCount   = await db.Messages.CountAsync(m => m.SeasonId == season && m.IsRead && !m.IsDeleted) + smsRead,
            TaskCount   = await db.MessageTasks.CountAsync(t => t.Message != null && t.Message.SeasonId == season && t.Status != MessageTaskStatus.Udført)
        };
        return View(vm);
    }

    // ── GetCounts ─────────────────────────────────────────────────
    public async Task<IActionResult> GetCounts()
    {
        var season = CurrentSeason;
        var (smsUnread, smsRead) = await GetSmsThreadCountsAsync(season);
        return Json(new
        {
            unreadCount = await db.Messages.CountAsync(m => m.SeasonId == season && !m.IsRead && !m.IsDeleted) + smsUnread,
            readCount   = await db.Messages.CountAsync(m => m.SeasonId == season && m.IsRead && !m.IsDeleted) + smsRead,
            taskCount   = await db.MessageTasks.CountAsync(t => t.Message != null && t.Message.SeasonId == season && t.Status != MessageTaskStatus.Udført)
        });
    }

    // ── Antal sms-tråde med/uden afventende ulæst indgående sms ────
    // Grupperingsnøglen (frivillig-id eller telefonnummer) er ikke SQL-oversætbar,
    // så sæsonens sms'er hentes til hukommelsen først (sæson-skala, uproblematisk).
    private async Task<(int Unread, int Read)> GetSmsThreadCountsAsync(int season)
    {
        var messages = await db.SmsMessages
            .Where(s => s.SeasonId == season)
            .Select(s => new { s.VolunteerId, s.PhoneNumberSnapshot, s.Direction, s.IsReadByCoordinator })
            .ToListAsync();

        var groups = messages.GroupBy(s => SmsThreadKey(s.VolunteerId, s.PhoneNumberSnapshot));
        var unread = groups.Count(g => g.Any(s => s.Direction == SmsDirection.Inbound && !s.IsReadByCoordinator));
        return (unread, groups.Count() - unread);
    }

    private static string SmsThreadKey(int? volunteerId, string phoneNumberSnapshot)
        => volunteerId.HasValue ? $"v{volunteerId}" : $"p{phoneNumberSnapshot}";

    // ── Partials ──────────────────────────────────────────────────
    public async Task<IActionResult> UnreadPartial(string q = "", int page = 1, int pageSize = 10)
        => await BuildThreadPartial(q, page, pageSize, isRead: false, "_UnreadPartial");

    public async Task<IActionResult> ReadPartial(string q = "", int page = 1, int pageSize = 10)
        => await BuildThreadPartial(q, page, pageSize, isRead: true, "_ReadPartial");

    public async Task<IActionResult> UnreadSearch(string q = "", int page = 1, int pageSize = 10)
        => await BuildThreadPartial(q, page, pageSize, isRead: false, "_UnreadPartial");

    public async Task<IActionResult> ReadSearch(string q = "", int page = 1, int pageSize = 10)
        => await BuildThreadPartial(q, page, pageSize, isRead: true, "_ReadPartial");

    // Bygger den samlede tråd-liste (app-beskeder + sms-samtaler) for enten
    // Ulæste- eller Læste-fanen. App-siden filtreres/søges DB-side som hidtil;
    // sms-siden grupperes pr. frivillig/nummer i hukommelsen (sæson-skala),
    // da grupperingsnøglen ikke er SQL-oversætbar. De to lister flettes,
    // sorteres efter seneste aktivitet og paginéres samlet.
    private async Task<IActionResult> BuildThreadPartial(string q, int page, int pageSize, bool isRead, string view)
    {
        var season = CurrentSeason;
        var ql = string.IsNullOrWhiteSpace(q) ? null : q.Trim().ToLower();

        // ── App-tråde ────────────────────────────────────────────
        var appQuery = db.Messages
            .Where(m => m.SeasonId == season && m.IsRead == isRead && !m.IsDeleted)
            .Include(m => m.Volunteer)
            .Include(m => m.Attachments)
            .Include(m => m.Tasks)
            .AsQueryable();

        if (ql is not null)
        {
            appQuery = appQuery.Where(m =>
                m.Subject.ToLower().Contains(ql) ||
                m.Volunteer.Name.ToLower().Contains(ql) ||
                m.Volunteer.Key.ToLower().Contains(ql));
        }

        var appMessages = await appQuery.ToListAsync();
        var appRows = appMessages.Select(m => new ThreadRowViewModel
        {
            Channel         = ThreadChannel.App,
            MessageId       = m.Id,
            VolunteerId     = m.VolunteerId,
            VolunteerName   = m.Volunteer.Name,
            VolunteerKey    = m.Volunteer.Key,
            Subject         = m.Subject,
            BodyPreview     = m.Body.Length > 80 ? m.Body[..80] + "…" : m.Body,
            LastActivityAt  = m.SentAt,
            ReadAt          = m.ReadAt,
            AttachmentCount = m.Attachments.Count,
            TaskCount       = m.Tasks.Count
        });

        // ── Sms-tråde ────────────────────────────────────────────
        var smsMessages = await db.SmsMessages
            .Where(s => s.SeasonId == season)
            .Include(s => s.Volunteer)
            .ToListAsync();

        var smsRows = smsMessages
            .GroupBy(s => SmsThreadKey(s.VolunteerId, s.PhoneNumberSnapshot))
            .Select(g =>
            {
                var last = g.OrderByDescending(s => s.OccurredAt).First();
                var hasPendingUnread = g.Any(s => s.Direction == SmsDirection.Inbound && !s.IsReadByCoordinator);
                var lastReadAt = g
                    .Where(s => s.Direction == SmsDirection.Inbound && s.ReadByCoordinatorAt.HasValue)
                    .OrderByDescending(s => s.ReadByCoordinatorAt)
                    .Select(s => s.ReadByCoordinatorAt)
                    .FirstOrDefault();
                var volunteer = g.Select(s => s.Volunteer).FirstOrDefault(v => v is not null);
                return new
                {
                    HasPendingUnread = hasPendingUnread,
                    Row = new ThreadRowViewModel
                    {
                        Channel         = ThreadChannel.Sms,
                        VolunteerId     = volunteer?.Id,
                        PhoneNumber     = volunteer is null ? last.PhoneNumberSnapshot : null,
                        VolunteerName   = volunteer?.Name ?? "Ukendt",
                        VolunteerKey    = volunteer?.Key ?? "",
                        Subject         = "SMS",
                        BodyPreview     = last.MessageBody.Length > 80 ? last.MessageBody[..80] + "…" : last.MessageBody,
                        LastActivityAt  = last.OccurredAt,
                        ReadAt          = lastReadAt,
                        AttachmentCount = 0,
                        TaskCount       = 0
                    }
                };
            })
            .Where(x => x.HasPendingUnread == !isRead)
            .Select(x => x.Row);

        if (ql is not null)
        {
            smsRows = smsRows.Where(r =>
                r.VolunteerName.ToLower().Contains(ql) ||
                r.VolunteerKey.ToLower().Contains(ql) ||
                (r.PhoneNumber ?? "").ToLower().Contains(ql) ||
                r.BodyPreview.ToLower().Contains(ql));
        }

        // ── Flet, sortér og paginér samlet ──────────────────────
        var combined = appRows.Concat(smsRows)
            .OrderByDescending(r => r.LastActivityAt)
            .ToList();

        var totalCount = combined.Count;
        if (pageSize < 1) pageSize = 10;
        if (page < 1) page = 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (page > totalPages && totalPages > 0) page = totalPages;

        var rows = combined.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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
            .Where(t => t.MessageId == null || t.Message!.SeasonId == season)
            .Include(t => t.Message)
                .ThenInclude(m => m!.Volunteer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(ql) ||
                (t.Message != null && t.Message.Volunteer.Name.ToLower().Contains(ql)) ||
                (t.Message != null && t.Message.Subject.ToLower().Contains(ql)));
        }

        var now = AppTime.Now;
        query = query
            .OrderBy(t =>
                t.Status != MessageTaskStatus.Udført && t.DueDate.HasValue && t.DueDate.Value < now ? 0 :
                t.Status == MessageTaskStatus.Åben ? 1 :
                t.Status == MessageTaskStatus.IGang ? 2 : 3)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt);

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
                Description    = GetTaskDescription(t.Description),
                Status         = t.Status,
                CreatedAt      = t.CreatedAt,
                DueDate        = t.DueDate,
                VolunteerName  = t.Message?.Volunteer?.Name ?? "—",
                VolunteerKey   = t.Message?.Volunteer?.Key ?? "",
                MessageSubject = t.Message?.Subject ?? "—"
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
        if (vm.VolunteerId == 0 || string.IsNullOrWhiteSpace(vm.Body))
            return Json(new { success = false, message = "Udfyld alle påkrævede felter." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        if (vm.Channel == ThreadChannel.Sms)
        {
            var result = await smsMessageLogService.SendAndLogAsync(vm.VolunteerId, vm.Body.Trim(), user.Id);
            return Json(new { success = result.Success, message = result.Success ? "Sms sendt." : result.ErrorMessage });
        }

        if (string.IsNullOrWhiteSpace(vm.Subject))
            return Json(new { success = false, message = "Udfyld alle påkrævede felter." });

        var msg = new Message
        {
            SeasonId      = CurrentSeason,
            VolunteerId   = vm.VolunteerId,
            SentByUserId  = user.Id,
            Direction     = MessageDirection.Outbound,
            Subject       = vm.Subject.Trim(),
            Body          = vm.Body.Trim(),
            IsRead        = true,   // Koordinator har selv skrevet den – allerede "læst" for koordinator
            ReadAt        = AppTime.Now,
            VolunteerOpenedAt = null, // Frivillig har ikke set den endnu
            SentAt        = AppTime.Now
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = "Besked oprettet." });
    }

    // ── Vis sms-tråd (én løbende samtale pr. frivillig/ukendt nummer) ─
    public async Task<IActionResult> SmsThreadDetail(int? volunteerId, string? phone)
    {
        var season = CurrentSeason;
        IQueryable<SmsMessage> query = db.SmsMessages.Where(s => s.SeasonId == season);
        query = volunteerId.HasValue
            ? query.Where(s => s.VolunteerId == volunteerId.Value)
            : query.Where(s => s.VolunteerId == null && s.PhoneNumberSnapshot == phone);

        var messages = await query
            .Include(s => s.Volunteer)
            .OrderBy(s => s.OccurredAt)
            .ToListAsync();

        if (messages.Count == 0) return NotFound();

        var toMarkRead = messages.Where(s => s.Direction == SmsDirection.Inbound && !s.IsReadByCoordinator).ToList();
        if (toMarkRead.Count > 0)
        {
            var now = AppTime.Now;
            foreach (var m in toMarkRead)
            {
                m.IsReadByCoordinator = true;
                m.ReadByCoordinatorAt = now;
            }
            await db.SaveChangesAsync();
        }

        var volunteer = messages.Select(s => s.Volunteer).FirstOrDefault(v => v is not null);
        var senderNames = new Dictionary<string, string>();
        foreach (var m in messages.Where(m => m.Direction == SmsDirection.Outbound && m.SentByUserId is not null))
        {
            if (senderNames.ContainsKey(m.SentByUserId!)) continue;
            var sender = await userManager.FindByIdAsync(m.SentByUserId!);
            senderNames[m.SentByUserId!] = sender?.DisplayName ?? sender?.UserName ?? "Koordinator";
        }

        var vm = new SmsThreadDetailViewModel
        {
            VolunteerId   = volunteer?.Id,
            PhoneNumber   = volunteer is null ? messages[0].PhoneNumberSnapshot : null,
            VolunteerName = volunteer?.Name ?? "Ukendt",
            VolunteerKey  = volunteer?.Key ?? "",
            Messages = messages.Select(m => new SmsThreadMessageViewModel
            {
                Id          = m.Id,
                Direction   = m.Direction,
                Body        = m.MessageBody,
                OccurredAt  = m.OccurredAt,
                SentByName  = m.Direction == SmsDirection.Outbound
                    ? (m.SentByUserId is not null ? senderNames.GetValueOrDefault(m.SentByUserId, "Koordinator") : "Koordinator")
                    : (volunteer?.Name ?? "Ukendt")
            }).ToList()
        };

        return PartialView("_SmsThreadDetailModal", vm);
    }

    // ── Besvar sms-tråd (koordinator) ──────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SmsReply([FromForm] int volunteerId, [FromForm] string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Json(new { success = false, message = "Svar kan ikke være tomt." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        var result = await smsMessageLogService.SendAndLogAsync(volunteerId, body.Trim(), user.Id);
        return Json(new { success = result.Success, message = result.Success ? "Sms sendt." : result.ErrorMessage });
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
            msg.ReadAt = AppTime.Now;
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
                SenderName = senderName,
                Latitude   = r.Latitude,
                Longitude  = r.Longitude
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
            SentAt       = AppTime.Now
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
            SentAt       = AppTime.Now
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
            msg.DeletedAt  = AppTime.Now;
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
            CreatedAt       = AppTime.Now,
            CreatedByUserId = user.Id,
            DueDate         = due
        });
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Opgave oprettet." });
    }

    // ── Opret standalone opgave (uden tilknyttet besked) ─────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStandaloneTask([FromForm] string title,
        [FromForm] string? description, [FromForm] string? dueDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Json(new { success = false, message = "Titel er påkrævet." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        DateTime? due = null;
        if (!string.IsNullOrWhiteSpace(dueDate) && DateTime.TryParse(dueDate, out var d))
            due = d;

        db.MessageTasks.Add(new MessageTask
        {
            MessageId       = null,
            Title           = title.Trim(),
            Description     = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status          = MessageTaskStatus.Åben,
            CreatedAt       = AppTime.Now,
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
            task.CompletedAt       = AppTime.Now;
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

    // ── Opdater opgave beskrivelse ───────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaskDescription([FromForm] int id, [FromForm] string? description)
    {
        var task = await db.MessageTasks.FindAsync(id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        var meta = ParseTaskMeta(task.Description);
        meta.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        task.Description = SerializeTaskMeta(meta);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = "Beskrivelse opdateret." });
    }

    [HttpGet]
    public async Task<IActionResult> GetTaskEditorData(int id)
    {
        var task = await db.MessageTasks.FindAsync(id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        var meta = ParseTaskMeta(task.Description);
        return Json(new
        {
            success = true,
            title = task.Title,
            description = meta.Description ?? string.Empty,
            dueDate = task.DueDate.HasValue ? task.DueDate.Value.ToString("yyyy-MM-dd") : string.Empty,
            status = (int)task.Status,
            notes = meta.Notes
                .OrderBy(n => n.CreatedAt)
                .Select(n => new { author = n.Author, note = n.Note, createdAt = n.CreatedAt.ToString("dd/MM HH:mm") })
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaskEditor([FromForm] int id, [FromForm] string? description, [FromForm] string? dueDate, [FromForm] MessageTaskStatus status)
    {
        var task = await db.MessageTasks.FindAsync(id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        var meta = ParseTaskMeta(task.Description);
        meta.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        task.Description = SerializeTaskMeta(meta);

        if (string.IsNullOrWhiteSpace(dueDate))
        {
            task.DueDate = null;
        }
        else if (DateTime.TryParse(dueDate, out var parsedDueDate))
        {
            task.DueDate = parsedDueDate;
        }

        task.Status = status;
        if (status == MessageTaskStatus.Udført)
        {
            var user = await userManager.GetUserAsync(User);
            task.CompletedAt = AppTime.Now;
            task.CompletedByUserId = user?.Id;
        }
        else
        {
            task.CompletedAt = null;
            task.CompletedByUserId = null;
        }

        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Opgave opdateret." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTaskEditorNote([FromForm] int id, [FromForm] string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return Json(new { success = false, message = "Noten må ikke være tom." });

        var task = await db.MessageTasks.FindAsync(id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Ikke logget ind." });

        var role = await userManager.IsInRoleAsync(user, "Administrator")
            ? "Administrator"
            : "Koordinator";

        var authorName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? (user.UserName ?? "Ukendt")
            : user.DisplayName;

        var meta = ParseTaskMeta(task.Description);
        meta.Notes.Add(new TaskLogNote
        {
            Author = $"{authorName} ({role})",
            Note = note.Trim(),
            CreatedAt = AppTime.Now
        });

        task.Description = SerializeTaskMeta(meta);
        await db.SaveChangesAsync();

        return Json(new { success = true, message = "Note gemt." });
    }

    // ── Slet opgave ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTask([FromForm] int id)
    {
        var task = await db.MessageTasks
            .Include(t => t.Message)
                .ThenInclude(m => m!.Tasks)
            .Include(t => t.Message)
                .ThenInclude(m => m!.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return Json(new { success = false, message = "Opgave ikke fundet." });

        var msg = task.Message;
        if (msg is null)
        {
            db.MessageTasks.Remove(task);
            await db.SaveChangesAsync();
            return Json(new { success = true, message = "Opgave slettet." });
        }

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

    private static string? GetTaskDescription(string? rawDescription)
        => ParseTaskMeta(rawDescription).Description;

    private static TaskMetaEnvelope ParseTaskMeta(string? rawDescription)
    {
        if (string.IsNullOrWhiteSpace(rawDescription))
            return new TaskMetaEnvelope();

        var markerIndex = rawDescription.IndexOf(TaskMetaMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return new TaskMetaEnvelope
            {
                Description = rawDescription
            };
        }

        var description = rawDescription[..markerIndex];
        var metaJson = rawDescription[(markerIndex + TaskMetaMarker.Length)..];

        try
        {
            var parsed = JsonSerializer.Deserialize<TaskMetaEnvelope>(metaJson) ?? new TaskMetaEnvelope();
            parsed.Description = description;
            parsed.Notes ??= new List<TaskLogNote>();
            return parsed;
        }
        catch
        {
            return new TaskMetaEnvelope
            {
                Description = rawDescription
            };
        }
    }

    private static string? SerializeTaskMeta(TaskMetaEnvelope meta)
    {
        var description = string.IsNullOrWhiteSpace(meta.Description) ? null : meta.Description.Trim();
        var notes = meta.Notes?.Where(n => !string.IsNullOrWhiteSpace(n.Note)).ToList() ?? new List<TaskLogNote>();

        if (!notes.Any())
            return description;

        var payload = new TaskMetaEnvelope
        {
            Description = null,
            Notes = notes
        };

        return (description ?? string.Empty) + TaskMetaMarker + JsonSerializer.Serialize(payload);
    }

    private sealed class TaskMetaEnvelope
    {
        public string? Description { get; set; }
        public List<TaskLogNote> Notes { get; set; } = new();
    }

    private sealed class TaskLogNote
    {
        public string Author { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}


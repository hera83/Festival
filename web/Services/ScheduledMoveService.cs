using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services.Sms;
using web.Utils;

namespace web.Services;

/// <summary>
/// Baggrundstjeneste der hvert 30. sekund tjekker om der er planlagte flytninger der skal udføres.
/// </summary>
public class ScheduledMoveService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledMoveService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public ScheduledMoveService(IServiceScopeFactory scopeFactory, ILogger<ScheduledMoveService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledMoveService startet.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMoves(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl i ScheduledMoveService.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingMoves(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var smsFlowService = scope.ServiceProvider.GetRequiredService<IDashboardSmsFlowService>();

        var now = AppTime.Now;

        var due = await db.ScheduledMoves
            .Include(m => m.Volunteer)
            .Where(m => !m.IsCancelled && m.ExecutedAt == null && m.ScheduledAt <= now)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        var seasonId = AppTime.CopenhagenToday.Year;
        var executedMoves = new List<(int VolunteerId, string VolunteerName, string From, string To)>();

        foreach (var move in due)
        {
            var checkIn = await db.VolunteerCheckIns
                .FirstOrDefaultAsync(c =>
                    c.SeasonId == seasonId &&
                    c.VolunteerId == move.VolunteerId &&
                    c.CheckedOutAt == null, ct);

            if (checkIn == null)
            {
                // Frivillig er ikke checket ind – marker som udført (kan ikke flyttes)
                move.ExecutedAt = now;
                _logger.LogInformation("ScheduledMove {Id}: {Name} er ikke checket ind – springer over.", move.Id, move.Volunteer?.Name);
                continue;
            }

            var from = checkIn.CurrentLocation;
            var to = move.TargetLocation;

            if (from != to)
            {
                checkIn.CurrentLocation = to;
                db.VolunteerLocationLogs.Add(new VolunteerLocationLog
                {
                    CheckInId = checkIn.Id,
                    VolunteerId = move.VolunteerId,
                    SeasonId = seasonId,
                    EventType = "Move",
                    Location = to,
                    OccurredAt = now
                });
                _logger.LogInformation("ScheduledMove {Id}: {Name} flyttet fra {From} til {To}.", move.Id, move.Volunteer?.Name, from, to);

                if (move.Volunteer != null)
                    executedMoves.Add((move.VolunteerId, move.Volunteer.Name, from, to));
            }

            move.ExecutedAt = now;
        }

        await db.SaveChangesAsync(ct);

        // Sms sendes først når flytningen er gemt, og kun for dem der reelt blev flyttet.
        foreach (var m in executedMoves)
        {
            await smsFlowService.SendTemplatedSmsAsync(
                SmsTemplateType.Moved, m.VolunteerId, m.VolunteerName, now,
                fraPost: m.From, tilPost: m.To, sentByUserId: "system", cancellationToken: ct);
        }
    }
}

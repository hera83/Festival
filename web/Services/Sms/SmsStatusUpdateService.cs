using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Services.Sms.Dtos.Sms;
using web.Utils;

namespace web.Services.Sms;

// Baggrundstjeneste der løbende følger op på sms'er der endnu ikke har fået et
// endeligt svar fra gatewayen (fx status "Queued"), og opdaterer den lokale
// log med den reelle status ved at slå op på MessageId.
public class SmsStatusUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISmsGatewayStatusCache _statusCache;
    private readonly ILogger<SmsStatusUpdateService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    // Statusser gatewayen ikke forventes at ændre igen — sms'er med en af disse
    // statusser slås ikke op igen.
    private static readonly HashSet<string> FinalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "sent", "delivered", "failed", "rejected", "error", "expired", "undelivered"
    };

    public SmsStatusUpdateService(IServiceScopeFactory scopeFactory, ISmsGatewayStatusCache statusCache, ILogger<SmsStatusUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _statusCache = statusCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SmsStatusUpdateService startet.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshGatewayStatus(stoppingToken);
                await CheckPendingStatuses(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl i SmsStatusUpdateService.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RefreshGatewayStatus(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sms = scope.ServiceProvider.GetRequiredService<ISmsService>();

        bool online;
        string? healthStatus = null;
        DateTime? healthTimestamp = null;
        string? errorMessage = null;
        decimal? balance = null;
        DateTime? balanceUpdatedAt = null;

        try
        {
            var health = await sms.GetHealthAsync(ct);
            online = health is not null;
            healthStatus = health?.Status;
            healthTimestamp = health?.Timestamp;
        }
        catch (HttpRequestException ex)
        {
            online = false;
            errorMessage = ex.Message;
        }

        try
        {
            var balanceResponse = await sms.GetBalanceCostAsync(cancellationToken: ct);
            balance = balanceResponse?.Balance;
            balanceUpdatedAt = balanceResponse?.UpdatedAt;
        }
        catch (HttpRequestException ex)
        {
            errorMessage ??= ex.Message;
        }

        _statusCache.Update(new SmsGatewayStatusSnapshot
        {
            Online = online,
            HealthStatus = healthStatus,
            HealthTimestamp = healthTimestamp,
            ErrorMessage = errorMessage,
            Balance = balance,
            BalanceUpdatedAt = balanceUpdatedAt
        });
    }

    private async Task CheckPendingStatuses(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sms = scope.ServiceProvider.GetRequiredService<ISmsService>();

        var season = AppTime.CurrentSeason;
        var candidates = await db.SmsMessages
            .Where(m => m.SeasonId == season)
            .ToListAsync(ct);

        // Kun udgående sms'er har et MessageId fra gatewayen og kan slås op.
        var pending = candidates.Where(m => m.Direction == SmsDirection.Outbound && m.MessageId.HasValue && !FinalStatuses.Contains(m.Status)).ToList();
        if (pending.Count == 0) return;

        var changed = false;
        foreach (var msg in pending)
        {
            GetStatusSmsResponseDto? status;
            try
            {
                status = await sms.GetSmsStatusAsync(new GetStatusSmsRequestDto { MessageId = msg.MessageId!.Value }, cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Kunne ikke hente status for sms {MessageId}.", msg.MessageId);
                continue;
            }

            if (status is null) continue;

            if (!string.Equals(msg.Status, status.Status, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Sms {MessageId}: status {Old} -> {New}.", msg.MessageId, msg.Status, status.Status);
                msg.Status = status.Status;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;
using web.Utils;

namespace web.Services.Sms
{
    public class DashboardSmsFlowService : IDashboardSmsFlowService
    {
        private readonly ApplicationDbContext _db;
        private readonly ISmsMessageLogService _smsMessageLogService;
        private readonly ISmsGatewayStatusCache _smsGatewayStatusCache;
        private readonly ILogger<DashboardSmsFlowService> _logger;

        public DashboardSmsFlowService(
            ApplicationDbContext db,
            ISmsMessageLogService smsMessageLogService,
            ISmsGatewayStatusCache smsGatewayStatusCache,
            ILogger<DashboardSmsFlowService> logger)
        {
            _db = db;
            _smsMessageLogService = smsMessageLogService;
            _smsGatewayStatusCache = smsGatewayStatusCache;
            _logger = logger;
        }

        public async Task SendTemplatedSmsAsync(
            SmsTemplateType type, int volunteerId, string volunteerName, DateTime when,
            string? post = null, string? fraPost = null, string? tilPost = null,
            string sentByUserId = "system", CancellationToken cancellationToken = default)
        {
            var seasonId = AppTime.CurrentSeason;

            var flowSetting = await _db.DashboardSettings
                .FirstOrDefaultAsync(s => s.SeasonId == seasonId && s.Key == SmsFlowSetting.Key, cancellationToken);
            if (flowSetting?.Value != "true")
                return;

            // Ekstra sikkerhedsnet: SmsStatusUpdateService slår flowet fra senest 15
            // sek. efter saldoen løber tør, men spring også denne enkelte sms over
            // hvis den friske cache allerede viser en for lav saldo.
            var status = _smsGatewayStatusCache.Current;
            if (status.Balance.HasValue &&
                (status.Balance.Value <= 0m || (status.SmsPriceDkk.HasValue && status.Balance.Value < status.SmsPriceDkk.Value)))
                return;

            try
            {
                // Kun frivillige hvis telefonnummer står på en AKTIV abonnementsliste må modtage sms.
                var eligibleIds = await _smsMessageLogService.GetEligibleVolunteerIdsAsync(seasonId, cancellationToken);
                if (!eligibleIds.Contains(volunteerId))
                    return;

                var template = await _db.SmsTemplates
                    .FirstOrDefaultAsync(t => t.SeasonId == seasonId && t.Type == type, cancellationToken);
                var body = template?.Body ?? SmsTemplateDefaults.For(type);

                var message = body
                    .Replace("{{Navn}}", volunteerName)
                    .Replace("{{Post}}", post ?? "")
                    .Replace("{{FraPost}}", fraPost ?? "")
                    .Replace("{{TilPost}}", tilPost ?? "")
                    .Replace("{{Tidspunkt}}", when.ToString("HH:mm"));

                await _smsMessageLogService.SendAndLogAsync(volunteerId, message, sentByUserId, cancellationToken);
            }
            catch (Exception ex)
            {
                // Sms-afsendelse må aldrig vælte den underliggende check-in/ud/flyt-handling.
                _logger.LogError(ex, "Kunne ikke sende skabelon-sms (type={Type}, volunteerId={VolunteerId}).", type, volunteerId);
            }
        }
    }
}

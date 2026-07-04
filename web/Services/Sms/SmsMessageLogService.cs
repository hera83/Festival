using web.Data;
using web.Models;
using web.Services.Sms.Dtos.Sms;
using web.Utils;

namespace web.Services.Sms
{
    public class SmsMessageLogService : ISmsMessageLogService
    {
        private readonly ISmsService _smsService;
        private readonly ApplicationDbContext _db;

        public SmsMessageLogService(ISmsService smsService, ApplicationDbContext db)
        {
            _smsService = smsService;
            _db = db;
        }

        public async Task<SmsSendResult> SendAndLogAsync(int volunteerId, string message, string sentByUserId, CancellationToken cancellationToken = default)
        {
            var volunteer = await _db.Volunteers.FindAsync([volunteerId], cancellationToken);
            if (volunteer is null)
            {
                return new SmsSendResult { Success = false, VolunteerId = volunteerId, ErrorMessage = "Frivillig ikke fundet." };
            }

            if (string.IsNullOrWhiteSpace(volunteer.PhoneNumber))
            {
                return new SmsSendResult { Success = false, VolunteerId = volunteerId, VolunteerName = volunteer.Name, ErrorMessage = "Frivillig har intet telefonnummer." };
            }

            // SMS-gatewayen kan kun sende til danske numre, og kun som et lokalt
            // 8-cifret nummer uden landekode — send derfor aldrig et nummer der
            // ikke normaliserer til præcis dét.
            if (!PhoneNumbers.TryNormalizeDanish(volunteer.PhoneNumber, out var normalizedPhone))
            {
                return new SmsSendResult { Success = false, VolunteerId = volunteerId, VolunteerName = volunteer.Name, ErrorMessage = "Telefonnummeret er ikke et gyldigt dansk nummer." };
            }

            SendSmsResponseDto? response;
            try
            {
                response = await _smsService.SendSmsAsync(new SendSmsRequestDto { To = normalizedPhone, Message = message }, cancellationToken: cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                return new SmsSendResult { Success = false, VolunteerId = volunteerId, VolunteerName = volunteer.Name, ErrorMessage = ex.Message };
            }

            if (response is null)
            {
                return new SmsSendResult { Success = false, VolunteerId = volunteerId, VolunteerName = volunteer.Name, ErrorMessage = "Tomt svar fra SMS-gateway." };
            }

            _db.SmsMessages.Add(new SmsMessage
            {
                SeasonId = volunteer.SeasonId,
                Direction = SmsDirection.Outbound,
                MessageId = response.MessageId,
                VolunteerId = volunteer.Id,
                PhoneNumberSnapshot = normalizedPhone,
                MessageBody = message,
                Status = response.Status,
                IsReadByCoordinator = true,
                SegmentCount = response.SegmentCount,
                UnitPriceDkk = response.UnitPriceDkk,
                TotalPriceDkk = response.TotalPriceDkk,
                OccurredAt = response.QueuedAt,
                SentByUserId = sentByUserId,
                CreatedAt = AppTime.Now
            });
            await _db.SaveChangesAsync(cancellationToken);

            return new SmsSendResult { Success = true, VolunteerId = volunteer.Id, VolunteerName = volunteer.Name, MessageId = response.MessageId };
        }
    }
}

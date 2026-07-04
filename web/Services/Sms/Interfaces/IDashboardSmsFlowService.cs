using web.Models;

namespace web.Services.Sms;

// Sender – hvis sms-flowet er slået til i dashboardet og den frivillige er på
// en aktiv abonnementsliste – den udfyldte skabelon for check-in/check-ud/flytning.
// Bruges både af DashboardController (manuelle handlinger) og ScheduledMoveService
// (planlagte flytninger, når de rent faktisk udføres).
public interface IDashboardSmsFlowService
{
    Task SendTemplatedSmsAsync(
        SmsTemplateType type, int volunteerId, string volunteerName, DateTime when,
        string? post = null, string? fraPost = null, string? tilPost = null,
        string sentByUserId = "system", CancellationToken cancellationToken = default);
}

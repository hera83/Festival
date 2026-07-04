namespace web.Services.Sms;

// Eneste tilladte vej til at sende en sms i systemet. Sikrer at MessageId fra
// gatewayen altid gemmes sammen med den frivillig-id sms'en blev sendt til.
public interface ISmsMessageLogService
{
    Task<SmsSendResult> SendAndLogAsync(int volunteerId, string message, string sentByUserId, CancellationToken cancellationToken = default);

    // Frivillig-id'er hvis telefonnummer er på en aktiv abonnementsliste hos sms-gatewayen
    // (i dag inden for start-/slutdato) — kun disse kan modtage sms og få deres svar matchet tilbage.
    Task<HashSet<int>> GetEligibleVolunteerIdsAsync(int seasonId, CancellationToken cancellationToken = default);
}

public sealed class SmsSendResult
{
    public bool Success { get; init; }
    public int VolunteerId { get; init; }
    public string VolunteerName { get; init; } = string.Empty;
    public Guid? MessageId { get; init; }
    public string? ErrorMessage { get; init; }
}
